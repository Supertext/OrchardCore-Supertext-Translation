# Developer guide — Supertext Translation for Orchard Core

How the module is built, how to work on it, and how the demo is deployed.

## Architecture

```
Content list "Supertext" / editor "Translate with Supertext"
   └─ GET/POST /Admin/Supertext/Translate/{contentItemId}   (AdminController)
        └─ per selected culture, one after the other:
             ContentTranslator.PrepareAsync(source)  ── HTML document ── SupertextClient
             existing ? GetAsync(DraftRequired) : IContentLocalizationManager.LocalizeAsync
             ContentTranslator.Apply → UpdateAsync → SaveDraftAsync

Orchard Core "Localizations → + culture" (ContentLocalization AdminController.Localize)
   └─ IContentLocalizationManager.LocalizeAsync → clone
        └─ SupertextLocalizationHandler.LocalizingAsync → ContentTranslator.TranslateAsync(original → clone)

Settings → Supertext (save)  → SupertextReporting.AfterSettingsSavedAsync  ── key check + report (in the request)
Settings page, Translate page, localization hook → SupertextReporting.OnRequestAsync ── after the response
Translation rejected (401/403) → SupertextReporting.AuthenticationFailedAsync ── after the response
GET /supertext/ping (public)  → PingController → VersionReports.Ping (Plugin Health PingResponse)
```

| Part | Role |
| --- | --- |
| `Manifest.cs`, `Startup.cs` | Module *Supertext Translation* (feature id `Supertext.OrchardCore.Translation`), depends on `OrchardCore.ContentLocalization` and `OrchardCore.Settings`. Registers the typed `HttpClient`, services, drivers, menu, permissions. |
| `Api/SupertextClient` | Wrapper around the bundled Supertext SDK (`lib/supertext-sdk`, see *Supertext SDK*): the protocol below, 429 retries, prefix-tolerant key, segments packed into documents below the size limit; SDK error codes become `SupertextException` templates. Stateless: each call takes a `SupertextConnection`. |
| `Sdk/…` (`lib/supertext-sdk/src`) | Bundled copy of the Supertext .NET SDK, namespace `Supertext.OrchardCore.Translation.Sdk` (`SdkInfo`) and `.Sdk.V1` (everything that talks to the API v1), compiled into the module. Never edit. |
| `PluginHealth/…` (`lib/supertext-plugin-health/src`) | Bundled copy of Supertext Plugin Health for .NET (see *Supertext Plugin Health*), namespace `Supertext.OrchardCore.Translation.PluginHealth`, compiled into the module. Never edit. |
| `Api/LanguageMapping` | Cultures → Supertext codes with the SDK's `Languages` (see *Language codes*); parses and writes the *Language mapping* setting; decides whether the form of address is sent. No Orchard dependency, unit-tested. |
| `Api/VersionReports` | Commando Center glue (see *Commando Center*): plugin/system names, site and callback URL, the setting with `SUPERTEXT_VERSION_REPORTS`, Plugin Health's `InstallationReporter` and `PingResponse` with the SDK's key check. No Orchard dependency, unit-tested. |
| `Services/SupertextReporting` | Runs the reporter with Orchard's data and keeps its state in the site document (`SupertextReportingState`); `OnRequestAsync`/`AuthenticationFailedAsync` run after the response (`HttpBackgroundJob`). |
| `Services/KeyCheckMessages` | The key check result in the admin's language (the center's message table, with links). |
| `Controllers/PingController` | `GET /supertext/ping`: public, answers exactly Plugin Health's `PingResponse`. |
| `Api/HtmlDocument` | Orchard's layer over the SDK's `HtmlDocument` (`<div data-st-id="N">` per segment; plain text HTML-encoded with line breaks as `<br>`; HTML fields as they are, **one segment per field**). Liquid (`{{ }}`, `{% %}`) is swapped for `<span data-st-keep="N"></span>` and restored; if a placeholder is lost, that field keeps the source. |
| `Services/ContentWalker` | Finds translatable strings in a content item's JSON using the type definition (`PartSchema`/`FieldSchema`), recursing into `FlowPart.Widgets`, `BagPart.ContentItems` and `WidgetsListPart.Widgets`. Pure JSON, unit-tested. |
| `Services/ContentTranslator` | Copies the source JSON (minus the target-owned parts `LocalizationPart`, `AutoroutePart`, `AliasPart`, `ContainedPart`), walks it, translates in documents of at most `MaxDocumentCharacters` (900 000), applies the result to the target with `ContentItem.Apply()` and sets `DisplayText`. |
| `Services/SupertextConfiguration` | Settings in effect: site settings (`SupertextSettings`, key protected with data protection purpose `Supertext.OrchardCore.Translation.ApiKey`), overridden by `SUPERTEXT_API_KEY` / `SUPERTEXT_API_ENDPOINT` or the tenant configuration `Supertext:ApiKey` / `Supertext:Endpoint`. Language mapping and exclusions parsing. |
| `Handlers/SupertextLocalizationHandler` | `IContentLocalizationHandler`; translates the clone during Orchard's own Localize action unless the setting is off or `SupertextScope.SuppressAutomaticTranslation` is set. Catches every error (Orchard's own localization always goes on) and tells the editor with a notification. |
| `Controllers/AdminController` | The *Translate with Supertext* page. Requires `TranslateWithSupertext`, `LocalizeContent` and `EditContent` on the source, and `EditContent` on an existing target. Overwriting needs `confirmOverwrite`. Translates first, creates/loads the target only on success, so a failure leaves nothing behind. |
| `Drivers/SupertextContentDisplayDriver` | Button shapes: `SupertextButton_SummaryAdmin` (`Actions:6`, next to *Localizations*) and `SupertextButton_Edit` (`Actions:35`). Only for saved items with a `LocalizationPart`. |
| `Drivers/SupertextSettingsDisplayDriver` | Settings group `supertext`; validates endpoint (https, or http to loopback) and timeout (30–3600 s); keeps the stored endpoint while `SUPERTEXT_API_ENDPOINT` (and the reporting setting while `SUPERTEXT_VERSION_REPORTS`) is set; the language mapping table (one row per culture, merged into the stored `culture=code` lines); after saving runs the key check and the report (`AfterSettingsSavedAsync`) and shows the result as a notification; on GET schedules `OnRequestAsync`. |
| `Services/SupertextMessages` | Shows a `SupertextException` in the admin's language: the exception's English `Template` is the msgid (context `…Services.SupertextMessages`), `Args` fill its placeholders, the untranslated `Detail` from Supertext is appended. `Message` stays English (logs). |
| `Localization/de.po`, `fr.po`, `it.po` | UI strings; see *UI strings* below. |
| `Permissions.cs` | `TranslateWithSupertext` (Administrator, Editor stereotypes), `ManageSupertextSettings` (Administrator). |

**What is translated** (by part/field type name): `TitlePart.Title`, `HtmlBodyPart.Html` (HTML), `MarkdownBodyPart.Markdown` (text), `TextField.Text` for the editors *Standard* (empty), *TextArea* and *Header*, `HtmlField.Html` except the *Monaco* editor, `MarkdownField.Markdown`, `LinkField.Text`, `MediaField.MediaTexts[]`. Values without words (URLs, e-mails, paths, numbers, anchors) are skipped (`ContentWalker.ShouldTranslate`).

**Why `Apply()`**: Orchard caches typed parts (e.g. `TitlePart`) per item. Writing JSON directly leaves stale typed parts, and `TitlePartHandler` would write the old title back on save. `ContentItem.Apply(replacement)` merges the JSON (arrays replaced) and clears the cache.

**Draft handling**: the Translate page translates the latest version (a draft if there is one). Existing targets get a new draft (`VersionOptions.DraftRequired`), so their published version stays online. Autoroute regenerates an empty path on `UpdateAsync`, so new localizations get a URL from the translated title.

### UI strings

All UI text goes through Orchard Core's localization: `T["…"]` in views, `S["…"]` (`IStringLocalizer<T>`) and `H["…"]` (`IHtmlLocalizer<T>`, notifications) in classes, and `SupertextException` messages through `SupertextMessages`. The translations are `Localization/de.po`, `fr.po` and `it.po` in the module: Orchard Core's `ModularPoFileLocationProvider` reads `Localization/<culture>.po` from each module (here embedded in the assembly by `OrchardCore.Module.Targets`), and `de-CH` falls back to `de`. English is the msgid itself.

- `msgctxt` is the class (`Supertext.OrchardCore.Translation.Controllers.AdminController`) or the view path (`Supertext.OrchardCore.Translation.Views.Admin.Translate`) that asks for the text; the same English text in two places needs two entries.
- **New or changed strings need all three .po files** in the same commit (English is the source): formal address (Sie, vous, Lei), Orchard Core's own terms in each language (from `OrchardCore.Translations.*`: *Entwurf*/*Brouillon*/*Bozza*, *Lokalisierungen*/*Localisations*/*Localizzazioni*, menu paths as the admin shows them), "Supertext", placeholders, HTML and URLs untranslated, a non-breaking space before French `?`, `!`, `:`, `;`.
- `LocalizationTests` fails when a text the code uses has no translation in a language, a .po file has entries no code uses, or a translation changes placeholders, tags or URLs.
- Permission descriptions are not localized: Orchard Core's *Roles* page shows them as registered.

## Supertext API protocol

AI file translation API v1, shared with all Supertext plugins. Every call goes through the Supertext SDK (see *Supertext SDK*); this is what it does:

1. `POST translate/ai/file` — multipart: `file` (`content.html`, part `Content-Type: text/html` exactly), `target_lang` (`de-CH`), `source_lang` (primary subtag, `en`), optional `politeness` (`more`/`less`) → `{file_id}`
2. `GET translate/ai/file/{id}/status` every 2 s until `done` (`error`, `limit_exceeded`, `deleted` fail; timeout from settings, default 300 s)
3. `GET translate/ai/file/{id}/translation` → translated HTML
4. `DELETE translate/ai/file/{id}` (files also expire after 24 h)

Header `Authorization: Supertext-Auth-Key <key>` (a pasted prefix is stripped, exactly one is sent). HTTP 429 is retried up to 4 times (`Retry-After`, else 1/2/4/8 s with jitter). Cultures are translated one after the other to stay under the per-second limit. The API key check is `GET features` (free), run by the SDK's `CheckApiKeyAsync` and reported by Supertext Plugin Health (see *Commando Center*).

## Supertext SDK

The SDK provides the API client and the API key check (`CheckApiKeyAsync`, `ApiKeyCheck.FingerprintOf`, `SdkInfo.Version`); version reports and the ping answer come from Supertext Plugin Health (next section). The protocol above, the retries, the language codes and the `data-st-id` document come from the official Supertext .NET SDK ([Supertext/Supertext-API-SDK-and-Guide](https://github.com/Supertext/Supertext-API-SDK-and-Guide)). The module compiles a copy from `lib/supertext-sdk/src/` (`SdkVersion.cs` and the `V1/` folder) into its own assembly (`<Compile Include="…/src/**/*.cs" Link="Sdk/…">` in the `.csproj`), with the namespace changed from `Supertext.Sdk` to `Supertext.OrchardCore.Translation.Sdk` (and `Supertext.Sdk.V1` to `…Translation.Sdk.V1`), so sites install nothing extra and another package that references the SDK itself can't clash with it. `lib/supertext-sdk/COMMIT` names the SDK commit and `VERSION` the SDK's package version. The SDK uses nullable annotations; the module keeps nullable off and silences CS8632 for them.

- Never edit `lib/supertext-sdk/`. Fix the SDK in its repository, then run `scripts/vendor-sdk.sh [ref]` (default `main`; `SDK_DIR=/path/to/clone` uses a local clone) and the tests.
- `Api/SupertextClient` turns every SDK error code into a `SupertextException` template (the arms of its switch, read by `LocalizationTests`), keeping the SDK's `Code` and HTTP `Status`. A new SDK error code needs an arm there and its entry in `de.po`, `fr.po` and `it.po`. `unsupported_language` names the culture; `unsupported_feature` is the form of address for a language without it.
- Segments missing from Supertext's answer (or whose Liquid came back damaged) keep the source text; the translator logs them.
- The live API answers an unknown language (`xx-YY`) with HTTP 404 `INVALID_LANGUAGE_PAIR`; the SDK reports it as `invalid_language_pair` with both codes, shown as "Supertext does not support this language pair (en → xx-YY)".
- The demo image copies `lib/` next to `src/` (see `demo/Dockerfile`).

## Supertext Plugin Health

Version reports to the Supertext Commando Center, the API key check result and the answer of the ping endpoint come from Supertext Plugin Health ([Commando-Center-Supertext-Translation → `plugin-health/`](https://github.com/Supertext/Commando-Center-Supertext-Translation/tree/main/plugin-health)), a dependency-free library to which the module passes the SDK's key check (`KeyChecker.FromDelegates` around `CheckApiKeyAsync` and `ApiKeyCheck.FingerprintOf`, in `Api/VersionReports.CreateReporter`). The module compiles a copy from `lib/supertext-plugin-health/src/` into its own assembly (`<Compile Include="…/supertext-plugin-health/src/**/*.cs" Link="PluginHealth/…">` in the `.csproj`), with the namespace changed from `Supertext.PluginHealth` to `Supertext.OrchardCore.Translation.PluginHealth`, exactly like the SDK copy. `lib/supertext-plugin-health/COMMIT` names the commit and `VERSION` the Plugin Health version.

- Never edit `lib/supertext-plugin-health/`. Fix it in Commando-Center-Supertext-Translation/plugin-health, then run `scripts/vendor-plugin-health.sh <tag>` (e.g. `plugin-health-v0.1.0`; default `main`; `PLUGIN_HEALTH_DIR=/path/to/clone` uses a local clone) and the tests.
- Its `ReporterState` JSON is the one the SDK 0.2 reporter stored, so sites that update keep their state and send no new `installed` report.

## Language codes

Supertext accepts exactly the codes its `GET features` lists (the SDK's `Languages`, from the live API): bare sources (`en`, `de`); targets in one exact form each, regional for German, English, French, Italian and Portuguese (`de-CH`, `en-US`, `pt-BR`), with a script for Chinese and Serbian (`zh-Hans`, `sr-Latn`), a dialect for Swiss German (`gsw-u-sd-chzh`), bare for the others (`es`, `nl`, `sl`). Anything else is HTTP 404 `INVALID_LANGUAGE_PAIR`; formal/informal for a target without it (`en-US`, `rm`, …) is HTTP 400 `FEATURE_ERROR` (`unsupported_feature`). The old default regions (`es-ES`, `sl-SI`, `zh-CN`) are rejected by the live API.

`Api/LanguageMapping` (no CMS dependency, unit-tested in `LanguageMappingTests`) resolves cultures **without guessing**, with **one** mapping for both directions (the *Language mapping* setting, `SupertextSettings.LanguageMapping`, `culture=code` per line, read by `SupertextConfiguration.Mapping`):

- **Target** (`LanguageMapping.Target` → SDK `Languages.Target`): the mapping wins (sent as it is, even a code the SDK doesn't list yet); else a culture equal to a Supertext code apart from case and `_`/`-` (`de-CH`, `fr_ch` → `fr-CH`, `es`); else `unsupported_language` (a `SupertextException` naming the culture), and nothing is sent. A culture `de` must be mapped (`de-DE`, `de-CH` or `de-AT`).
- **Source** (`LanguageMapping.Source` → SDK `Languages.Source`): the mapping in its source form (`de` → `de-CH` → `de`), else the culture's language (`en-US` → `en`), else `unsupported_language`. Empty = auto-detect.
- **Form of address** (`LanguageMapping.PolitenessFor`): sent only for targets with `Languages.SupportsPoliteness` ≠ false. The setting is one value for all languages, so for English, Swiss German, Romansh, Albanian and Serbian the module sends the default tone instead of failing; the settings page says so next to those languages.
- **Settings page**: one row per site culture with a select of `Languages.Targets` (name + code), pre-selected with the stored mapping, else the exact match; *Not mapped* and a warning when neither exists; a stored code that isn't in the list stays selectable and is flagged. Saving merges the rows into the stored lines (entries for cultures the site no longer has are kept).
- The demo maps its source culture `en` to `en-US` (`DemoSetupEvents.EnsureLanguageMappingAsync`, only while the mapping is empty); `de-CH`, `fr-CH`, `it-CH` are Supertext codes.

The stand-in (`Tests/Docs/stand-in.mjs`) accepts exactly the live API's target codes and rejects the others with 404 `INVALID_LANGUAGE_PAIR`.

## Commando Center: version reports, key check, ping

Protocol: [Commando-Center-Supertext-Translation](https://github.com/Supertext/Commando-Center-Supertext-Translation) → `docs/DEVELOPER.md` → *Report protocol*. Supertext Plugin Health's `InstallationReporter` and `PingResponse` implement it, with the SDK's `CheckApiKeyAsync()` as the key check; `Api/VersionReports` and `Services/SupertextReporting` only supply what Orchard Core knows:

- **Setting:** `SupertextSettings.SendVersionInformation` (default on), combined with `SUPERTEXT_VERSION_REPORTS` by Plugin Health's `Reporting.ReportsEnabled` (the environment wins; the checkbox is then read-only). Off: no reports, ping 404. `SUPERTEXT_REPORT_URL` sends reports elsewhere (tests, screenshots).
- **Report fields:** `site_url` = **Settings → General → Base URL** (`ISite.BaseUrl`) if absolute, else the request's scheme, host and path base (tenant prefix); plugin `OrchardCore-Supertext-Translation` with `ModuleVersion.Current`; system `Orchard Core` with the informational version of `OrchardCore.Abstractions` (the running Orchard Core); runtime `.NET` is Plugin Health's default, `sdk_version` the bundled SDK's `SdkInfo.Version`; `callback.url` = `<site>/supertext/ping`.
- **When:** `AfterSettingsSavedAsync` inside the settings save (key check, `installed`/`settings_saved`; the result is shown as a notification and kept for the page). `OnRequestAsync` on GET of the settings page, the Translate page (GET and POST) and in the localization hook, at most once per request (`SupertextScope.ReportScheduled`), run by `HttpBackgroundJob.ExecuteAfterEndOfRequestAsync` in its own shell scope after the response; it checks the key only when its fingerprint changed and reports `installed`, `upgraded` or `api_key_check`. `AuthenticationFailedAsync` (once per request) when a translation gets `authentication_failure` (401/403). There is no *Test connection* button and no CLI command, so no `testConnection`/`reportCheck`. Runs are serialized per process (a semaphore). Nothing here throws into a request.
- **State:** Plugin Health's `ReporterState` JSON (the same format the SDK 0.2 reporter stored) in the site document under `SupertextReportingState.Reporter` (`site.Alter<>`; saved with the settings on save, with `UpdateSiteSettingsAsync` in the background job only when it changed). It holds a SHA-256 fingerprint of the key, never the key.
- **Ping:** `Controllers/PingController`, attribute route `GET supertext/ping` (relative to the tenant), `[AllowAnonymous]`, no antiforgery; status, headers (`Content-Type: application/json`, `Cache-Control: no-store`) and body exactly as `PingResponse.For` returns them. Orchard Core has no output cache for controller actions, so nothing caches it.

## Local development

.NET 10 SDK and Node 20+ (stand-in API, screenshots).

```bash
cd Tests/Docs && npm install && STAND_IN_PREFIX=1 node stand-in.mjs &   # untranslated text comes back as "[de-CH] …"
cd demo/SupertextDemo
export DEMO_ADMIN_EMAIL=admin@example.com DEMO_ADMIN_PASSWORD='Choose-a-pass-1!' \
       DEMO_EDITOR_EMAIL=editor@example.com DEMO_EDITOR_PASSWORD='Choose-a-pass-2!' \
       OrchardCore__OrchardCore_AutoSetup__Tenants__0__AdminUsername=admin \
       OrchardCore__OrchardCore_AutoSetup__Tenants__0__AdminEmail=admin@example.com \
       OrchardCore__OrchardCore_AutoSetup__Tenants__0__AdminPassword='Choose-a-pass-1!' \
       SUPERTEXT_API_KEY=test SUPERTEXT_API_ENDPOINT=http://127.0.0.1:8765/v1/ \
       SUPERTEXT_REPORT_URL=http://127.0.0.1:8765/report
dotnet run --urls http://127.0.0.1:8096
```

The first request installs the site (SQLite in `App_Data`) from `Recipes/supertext-demo.recipe.json`; `DemoSetupEvents` then logs `[demo] setup complete`. Delete `App_Data` for a fresh start. Sign in at `/Login` with the e-mail address.

If a Razor view suddenly fails with errors at impossible positions after editing, run `dotnet build-server shutdown` and delete the project's `obj/` folder (stale source generator cache).

## Tests

```bash
dotnet test tests/Supertext.OrchardCore.Translation.Tests   # SDK wrapper against a fake API, HTML packing, Liquid protection, content walker, settings parsing, language mapping, version reports, module version, UI strings in de/fr/it
```

- `LanguageMappingTests`: mapping, exact codes, `unsupported_language` for `de`/`es-ES`/`zh-CN` without mapping, source forms, form of address only where Supertext offers it.
- `VersionReportsTests` (fake HTTP, never the live API or center): the setting and `SUPERTEXT_VERSION_REPORTS`, site URL, ping answers (200/400/404), one `installed` report with the right fields and without the key, nothing on an unchanged request, `upgraded`, `settings_saved`, a 401/403 reported once, nothing sent with reports off, no exception when the network fails, state stored in a site entity.

CI (`.github/workflows/ci.yml`) builds the module and the demo, runs the tests, checks the demo entrypoint and the Node scripts' syntax. `.github/workflows/e2e.yml` runs the end-to-end test against the real Supertext API and regenerates the docs screenshots (see *End-to-end tests*).

Manual check (before a release): fresh demo with the stand-in (`STAND_IN_PREFIX=1`) and `SUPERTEXT_REPORT_URL=http://127.0.0.1:8765/report` (`REPORT_LOG=reports.jsonl` on the stand-in records the reports), check `GET /supertext/ping?challenge=abcdefghijklmnop` (200 with the challenge) and the `installed` report after saving Settings → Supertext, sign in as the editor, translate *Translating with Supertext* into all three languages (all texts carry the marker, the link URL doesn't), translate German again (overwrite warning), publish German and open its URL; create the German version of *About this demo* from the *Localizations* menu (the Flow widgets are translated). `Tests/Docs/screenshots.mjs` runs most of this.

## End-to-end tests

The unit tests use fakes. `.github/workflows/e2e.yml` also tests against the **real Supertext API**, so changes on Supertext's side (language codes, regional variants, formatting, rate limits) show up. It runs on every push to `main` that touches the module (`src/`, `lib/`), the demo or `Tests/`, on pull requests that change the E2E test or workflow, every Monday (05:39 UTC), and by hand (*Actions → E2E → Run workflow*). It builds the demo image once (Docker layer cache in GitHub Actions) and starts it twice, each with a fresh `App_Data` and demo accounts made up for the run:

1. **Real API** (repository or organization secret `SUPERTEXT_API_KEY`, set by Remy; without it these steps are skipped with a warning). Version reports are off (`SUPERTEXT_VERSION_REPORTS=0`), so CI runs don't appear in the Commando Center. `/data` is a folder on the runner, so the test can read the SQLite database.
   - `Tests/E2E/features.mjs` asks the live `GET features` (cost-free, no module or SDK code) whether every pair the demo translates (`en` → `de-CH`, `fr-CH`, `it-CH`) is offered; a missing pair fails the run. Whether the module's own language list matches the API is the SDK's job (its own tests).
   - `Tests/E2E/live.mjs` (Playwright, as the `DEMO_EDITOR_*` account, through Orchard Core's admin): the editor first creates one more English article whose HTML body contains Liquid (`{{ Site.SiteName }}`, `{% if … %}…{% endif %}`), because the demo's sample content has none. Then every English item (*Translating with Supertext*, *About this demo* with its Flow widgets, the Liquid article) is translated into every culture with the editor's **Translate with Supertext** button. The database (YesSql's `ContentItemIndex` and `Document`) is checked: every item has exactly one localization per culture, none of them is published, every text field the module translates is filled and at least 80 % of them changed (a single word can stay the same), no `data-st-id`/`data-st-keep`/placeholder is left, links, bold, italics, list items, headings and Liquid tags are kept (Liquid exactly), the text is in the target language (common words of each language counted) and de-CH has no "ß". The page shows one success message per culture and no warning ("kept the source wording") or error. Translating again (overwrite confirmed) must update the same localizations, not create new ones, and publish nothing. Each run translates three items twice into three languages (a few thousand characters on the key's account).
2. **Stand-in API**: the docs screenshots (see *Docs screenshots*), so the images don't change with each machine translation. If `docs/images/` changed, a second job pushes the branch `automation/docs-screenshots` and opens (or updates) the pull request "Docs: regenerate screenshots". This needs *Settings → Actions → General → Allow GitHub Actions to create and approve pull requests*.

If a run fails, the artifact `e2e-debug` has the result pages (`Tests/E2E/artifacts/`), the demo logs and the stand-in log. Locally (Node 22.13 or later, for `node:sqlite`):

```bash
cd Tests/E2E && npm install && npx playwright install chromium
# fresh demo with a real SUPERTEXT_API_KEY and its /data (App_Data) on this machine, e.g. on port 8081:
#   docker run -d --network host -v "$PWD/demo-data:/data" -e PORT=8081 -e SUPERTEXT_API_KEY -e SUPERTEXT_VERSION_REPORTS=0 \
#     -e DEMO_ADMIN_EMAIL -e DEMO_ADMIN_PASSWORD -e DEMO_EDITOR_EMAIL -e DEMO_EDITOR_PASSWORD supertext-orchardcore-demo
#   sudo chmod -R a+rwX demo-data   # the container writes as root
SUPERTEXT_API_KEY=… E2E_TARGETS=de-CH,fr-CH,it-CH node features.mjs
ORCHARD_URL=http://127.0.0.1:8081 ORCHARD_DATA="$PWD/demo-data" EDITOR_EMAIL=… EDITOR_PASSWORD=… npm run live
```

To try changes to the test without a key, run it against a demo that talks to the stand-in (`SUPERTEXT_API_KEY=stand-in`, `SUPERTEXT_API_ENDPOINT=http://127.0.0.1:8765/v1/`): the flow and the structural checks run, but the stand-in only knows German for the sample texts, so the translation and language checks fail for the other texts and cultures. Sessions never use a live key.

## Docs screenshots

`docs/images/` is generated by `Tests/Docs/screenshots.mjs` from a **fresh** local demo whose module talks to the stand-in (without `STAND_IN_PREFIX`). The stand-in returns real German for the sample content (`Tests/Docs/sample-de.json`), so the guides never show placeholder text.

Start the demo with `SUPERTEXT_API_ENDPOINT` pointing to the stand-in and `SUPERTEXT_REPORT_URL=http://127.0.0.1:8765/report` (the stand-in answers reports with 202, so screenshots never report to the live center), but **without** `SUPERTEXT_API_KEY`: the script stores a dummy key in **Settings → Supertext** like an administrator (the stand-in accepts any key), and hides the endpoint hint, which would show the local URL.

```bash
cd Tests/Docs && npm install && npx playwright install chromium && node stand-in.mjs &
# start the demo fresh (see Local development, minus SUPERTEXT_API_KEY), then:
BASE_URL=http://127.0.0.1:8096 DEMO_ADMIN_EMAIL=… DEMO_ADMIN_PASSWORD=… DEMO_EDITOR_EMAIL=… DEMO_EDITOR_PASSWORD=… npm run screenshots
```

The script shows fixed values for server-rendered times ("1 minute ago" in the content list, the key check's date), so the images only change when the UI does. In CI, the E2E workflow takes the screenshots on every run and opens a pull request when they differ (see *End-to-end tests*).

New texts in the flow: run the stand-in with `STAND_IN_DUMP=<dir>` and translate what it writes to `<dir>/de-CH.json` into `sample-de.json`. `CHROMIUM_PATH` points Playwright at an installed Chromium.

## Demo (Railway)

The public demo is a container built from `demo/Dockerfile`: Orchard Core 3.0.1 with TheBlogTheme, English with German, French and Italian (Switzerland), and this module by project reference. It runs on Railway in the `supertext-cms-demos` project, service `orchardcore`, region EU West (Amsterdam): <https://orchardcore-production.up.railway.app/> (admin: `/Admin`, sign in at `/Login` with the e-mail address).

**Deploys:** Railway watches `main` and rebuilds on every push.

| File | Purpose |
| --- | --- |
| `demo/Dockerfile` | `dotnet publish` of `demo/SupertextDemo` (SDK image), run on the ASP.NET image with `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` |
| `demo/entrypoint.sh` | Links `/app/App_Data` to the volume (`/data/App_Data`); maps `DEMO_ADMIN_*` to the AutoSetup admin (user name = the e-mail's local part, since Orchard user names can't contain `@`) |
| `demo/SupertextDemo/Program.cs` | `AddOrchardCms()` with the AutoSetup feature and the demo setup; a warm-up request after start runs the first-boot setup without waiting for a visitor |
| `demo/SupertextDemo/Recipes/supertext-demo.recipe.json` | Setup recipe: features, TheBlogTheme, cultures `en` (default), `de-CH`, `fr-CH`, `it-CH`, roles, the types *Article* (title, subtitle, banner image, HTML body) and *Page* (Flow with *Paragraph* and *Blockquote* widgets), both with Localization and an Autoroute pattern that prefixes the culture (`en/…`, `de/…`), sample content, main menu |
| `demo/SupertextDemo/DemoSetup/DemoSetupEvents.cs` | On every start of the running tenant: adds missing cultures, maps `en` to the Supertext language `en-US` while the language mapping is empty, gives the *Editor* role what it needs (`LocalizeContent`, `TranslateWithSupertext`, edit/publish/preview/view content, admin access), creates the demo accounts, creates an English sample article if there is none |
| `demo/.env.example` | All variables |

**State:** the SQLite database, media and the data protection keys (which encrypt the stored API key) live in `App_Data` on a Railway volume at `/data`. To reset the demo, delete the files on the volume and redeploy.

**Accounts** (created on every start if missing; existing ones are never changed; Orchard Core's password rules: at least 6 characters with an upper-case letter, a lower-case letter, a digit and a symbol — a password that doesn't meet them skips the account with a warning in the log naming the variable):

| Variables | Account |
| --- | --- |
| `DEMO_ADMIN_EMAIL`, `DEMO_ADMIN_PASSWORD` (fallback `ORCHARD_ADMIN_*`) | *Administrator* role; also the AutoSetup admin on first boot (required then — the container exits without it, and an invalid password makes the setup fail; see the log) |
| `DEMO_EDITOR_EMAIL`, `DEMO_EDITOR_PASSWORD` | *Editor* role — can edit, translate and publish content in all cultures |

Orchard Core's setup screen never appears: AutoSetup installs the site.

**Service variables:** `DEMO_*` (above), `SUPERTEXT_API_KEY`, optional `SUPERTEXT_API_ENDPOINT` and `SUPERTEXT_VERSION_REPORTS` (`0` turns the version reports off), `PORT=8080` (the domain's target port), `RAILWAY_DOCKERFILE_PATH=demo/Dockerfile`. On Railway the `DEMO_*` variables reference the umbraco service's (`${{umbraco.DEMO_ADMIN_EMAIL}}` …), so all .NET demos share one set.

**Run it locally:**

```bash
docker build -f demo/Dockerfile -t supertext-orchardcore-demo .
docker run --rm -p 8080:8080 -v orcharddemo:/data \
  -e DEMO_ADMIN_EMAIL=admin@example.com -e DEMO_ADMIN_PASSWORD='Choose-a-pass-1!' \
  -e SUPERTEXT_API_KEY=... supertext-orchardcore-demo
```

## Code quality and security checks

- **Checks** workflow (`.github/workflows/checks.yml`): [actionlint](https://github.com/rhysd/actionlint) and [zizmor](https://docs.zizmor.sh/) lint the workflows on every push and pull request. Dependency review fails a pull request that adds a package with a known vulnerability (moderate or worse). Actions are pinned to commit SHAs; Dependabot keeps the pins up to date. To run the workflow lint locally: `pip install actionlint-py zizmor`, then `actionlint` and `zizmor .github/workflows` in the repo root.
- **Links** workflow (`.github/workflows/links.yml`): [lychee](https://lychee.cli.rs/) checks the links in all Markdown files weekly and whenever docs change on `main`. Broken links open (or update) the issue "Broken links in the docs". Links that can't work from CI (local URLs, placeholders, pages behind a login) are excluded in `.lycheeignore`.
- C# and the JavaScript are analysed by CodeQL (see below), so this repo runs no separate static analyser.
- GitHub settings (set by Remy's setup script, not stored in the repo): **secret scanning with push protection** (a push containing a known token format is rejected; findings under *Security → Secret scanning*) and **CodeQL default setup** (findings under *Security → Code scanning* and as comments on pull requests; PHP isn't covered by CodeQL, which is why the PHP plugins run PHPStan).

Before starting work in this repo, look at its open findings: code scanning alerts, secret scanning alerts, Dependabot PRs and the "Broken links in the docs" issue.

## Releasing

Releases are published by `.github/workflows/release.yml` when the version is officially bumped; nobody tags or creates releases by hand.

1. Move the *Unreleased* entries in `CHANGELOG.md` under a new `## X.Y.Z — YYYY-MM-DD` section, and keep an empty *Unreleased* above it.
2. Set the same version in:
   - `src/Supertext.OrchardCore.Translation/Supertext.OrchardCore.Translation.csproj`: `Version`, the NuGet package version
   - `src/Supertext.OrchardCore.Translation/Manifest.cs`: the module's `Version`

   Settings → Supertext shows the assembly's informational version (from the `.csproj` `Version`, build metadata after `+` stripped; `Services/ModuleVersion.cs`) and links X.Y.Z to its GitHub release; there is no other copy to update.
3. Push to `main`. The workflow checks that the version files match `CHANGELOG.md`, then tags `vX.Y.Z` and creates the GitHub release with the CHANGELOG section as notes (0.x versions as pre-releases). A push that adds no new version does nothing, and a version that is already released is skipped. After fixing a failed run, start it again with *Run workflow* on the *Release* workflow.

Publishing to NuGet stays manual: `dotnet pack src/Supertext.OrchardCore.Translation -c Release`, then push the package (tagged `OrchardCoreCMS` and `Module`, so it is found as an Orchard Core module).
## Known limitations / roadmap

- Translation runs inside the request (up to the timeout per culture). Planned: a background task with progress for very long items and many cultures.
- Unsaved changes in the editor are not translated (the button's tooltip says so).
- No sync of later source changes; translate again to refresh a culture.
- Markdown is sent as plain text: syntax usually survives, but isn't protected.
- Shortcodes (`[image]…`) in HTML are sent as text; only Liquid is protected.
- Taxonomies, tags, menus, content pickers and media files are not translated; custom part properties (not fields) of third-party parts are not translated.
- Permission names on the *Roles* page stay English (Orchard Core doesn't localize permission descriptions).
- Human (professional) translation orders are not supported yet.
- Tested on Orchard Core 3.0.1 with the stand-in API; not yet against the live API.
