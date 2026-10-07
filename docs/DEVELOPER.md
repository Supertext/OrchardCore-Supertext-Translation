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
```

| Part | Role |
| --- | --- |
| `Manifest.cs`, `Startup.cs` | Module *Supertext Translation* (feature id `Supertext.OrchardCore.Translation`), depends on `OrchardCore.ContentLocalization` and `OrchardCore.Settings`. Registers the typed `HttpClient`, services, drivers, menu, permissions. |
| `Api/SupertextClient` | HTTP protocol (below): multipart upload with quoted part names, polling, download, delete; 429 retries; prefix-tolerant key. Stateless: each call takes a `SupertextConnection`. |
| `Api/HtmlDocument` | Packs segments into one HTML document (`<div data-st-id="N">`) and back. Plain text is HTML-encoded (line breaks as `<br>`); HTML fields go as they are, **one segment per field**. Liquid (`{{ }}`, `{% %}`) is swapped for `<span data-st-keep="N"></span>` and restored; if a placeholder is lost, that field keeps the source. |
| `Services/ContentWalker` | Finds translatable strings in a content item's JSON using the type definition (`PartSchema`/`FieldSchema`), recursing into `FlowPart.Widgets`, `BagPart.ContentItems` and `WidgetsListPart.Widgets`. Pure JSON, unit-tested. |
| `Services/ContentTranslator` | Copies the source JSON (minus the target-owned parts `LocalizationPart`, `AutoroutePart`, `AliasPart`, `ContainedPart`), walks it, translates in documents of at most `MaxDocumentCharacters` (900 000), applies the result to the target with `ContentItem.Apply()` and sets `DisplayText`. |
| `Services/SupertextConfiguration` | Settings in effect: site settings (`SupertextSettings`, key protected with data protection purpose `Supertext.OrchardCore.Translation.ApiKey`), overridden by `SUPERTEXT_API_KEY` / `SUPERTEXT_API_ENDPOINT` or the tenant configuration `Supertext:ApiKey` / `Supertext:Endpoint`. Language mapping and exclusions parsing. |
| `Handlers/SupertextLocalizationHandler` | `IContentLocalizationHandler`; translates the clone during Orchard's own Localize action unless the setting is off or `SupertextScope.SuppressAutomaticTranslation` is set. Catches every error (Orchard's own localization always goes on) and tells the editor with a notification. |
| `Controllers/AdminController` | The *Translate with Supertext* page. Requires `TranslateWithSupertext`, `LocalizeContent` and `EditContent` on the source, and `EditContent` on an existing target. Overwriting needs `confirmOverwrite`. Translates first, creates/loads the target only on success, so a failure leaves nothing behind. |
| `Drivers/SupertextContentDisplayDriver` | Button shapes: `SupertextButton_SummaryAdmin` (`Actions:6`, next to *Localizations*) and `SupertextButton_Edit` (`Actions:35`). Only for saved items with a `LocalizationPart`. |
| `Drivers/SupertextSettingsDisplayDriver` | Settings group `supertext`; validates endpoint (https, or http to loopback) and timeout (30–3600 s); keeps the stored endpoint while `SUPERTEXT_API_ENDPOINT` is set; checks the key with `GET features` after saving. |
| `Permissions.cs` | `TranslateWithSupertext` (Administrator, Editor stereotypes), `ManageSupertextSettings` (Administrator). |

**What is translated** (by part/field type name): `TitlePart.Title`, `HtmlBodyPart.Html` (HTML), `MarkdownBodyPart.Markdown` (text), `TextField.Text` for the editors *Standard* (empty), *TextArea* and *Header*, `HtmlField.Html` except the *Monaco* editor, `MarkdownField.Markdown`, `LinkField.Text`, `MediaField.MediaTexts[]`. Values without words (URLs, e-mails, paths, numbers, anchors) are skipped (`ContentWalker.ShouldTranslate`).

**Why `Apply()`**: Orchard caches typed parts (e.g. `TitlePart`) per item. Writing JSON directly leaves stale typed parts, and `TitlePartHandler` would write the old title back on save. `ContentItem.Apply(replacement)` merges the JSON (arrays replaced) and clears the cache.

**Draft handling**: the Translate page translates the latest version (a draft if there is one). Existing targets get a new draft (`VersionOptions.DraftRequired`), so their published version stays online. Autoroute regenerates an empty path on `UpdateAsync`, so new localizations get a URL from the translated title.

## Supertext API protocol

AI file translation API v1, shared with all Supertext plugins:

1. `POST translate/ai/file` — multipart: `file` (`content.html`, part `Content-Type: text/html` exactly), `target_lang` (`de-CH`), `source_lang` (primary subtag, `en`), optional `politeness` (`more`/`less`) → `{file_id}`
2. `GET translate/ai/file/{id}/status` every 2 s until `done` (`error`, `limit_exceeded`, `deleted` fail; timeout from settings, default 300 s)
3. `GET translate/ai/file/{id}/translation` → translated HTML
4. `DELETE translate/ai/file/{id}` (files also expire after 24 h)

Header `Authorization: Supertext-Auth-Key <key>` (a pasted prefix is stripped, exactly one is sent). HTTP 429 is retried up to 4 times (`Retry-After`, else 1/2/4/8 s with jitter). Cultures are translated one after the other to stay under the per-second limit. The settings page calls `GET features` (free) to check the key.

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
       SUPERTEXT_API_KEY=test SUPERTEXT_API_ENDPOINT=http://127.0.0.1:8765/v1/
dotnet run --urls http://127.0.0.1:8096
```

The first request installs the site (SQLite in `App_Data`) from `Recipes/supertext-demo.recipe.json`; `DemoSetupEvents` then logs `[demo] setup complete`. Delete `App_Data` for a fresh start. Sign in at `/Login` with the e-mail address.

If a Razor view suddenly fails with errors at impossible positions after editing, run `dotnet build-server shutdown` and delete the project's `obj/` folder (stale source generator cache).

## Tests

```bash
dotnet test tests/Supertext.OrchardCore.Translation.Tests   # HTML packing, Liquid protection, content walker, settings parsing, module version
```

CI (`.github/workflows/ci.yml`) builds the module and the demo, runs the tests, checks the demo entrypoint and the Node scripts' syntax.

End to end (manual, before a release): fresh demo with the stand-in (`STAND_IN_PREFIX=1`), sign in as the editor, translate *Translating with Supertext* into all three languages (all texts carry the marker, the link URL doesn't), translate German again (overwrite warning), publish German and open its URL; create the German version of *About this demo* from the *Localizations* menu (the Flow widgets are translated). `Tests/Docs/screenshots.mjs` runs most of this.

## Docs screenshots

`docs/images/` is generated by `Tests/Docs/screenshots.mjs` from a **fresh** local demo whose module talks to the stand-in (without `STAND_IN_PREFIX`). The stand-in returns real German for the sample content (`Tests/Docs/sample-de.json`), so the guides never show placeholder text.

Start the demo with `SUPERTEXT_API_ENDPOINT` pointing to the stand-in but **without** `SUPERTEXT_API_KEY`: the script stores a dummy key in **Settings → Supertext** like an administrator (the stand-in accepts any key), and hides the endpoint hint, which would show the local URL.

```bash
cd Tests/Docs && npm install && npx playwright install chromium && node stand-in.mjs &
# start the demo fresh (see Local development, minus SUPERTEXT_API_KEY), then:
BASE_URL=http://127.0.0.1:8096 DEMO_ADMIN_EMAIL=… DEMO_ADMIN_PASSWORD=… DEMO_EDITOR_EMAIL=… DEMO_EDITOR_PASSWORD=… npm run screenshots
```

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
| `demo/SupertextDemo/DemoSetup/DemoSetupEvents.cs` | On every start of the running tenant: adds missing cultures, gives the *Editor* role what it needs (`LocalizeContent`, `TranslateWithSupertext`, edit/publish/preview/view content, admin access), creates the demo accounts, creates an English sample article if there is none |
| `demo/.env.example` | All variables |

**State:** the SQLite database, media and the data protection keys (which encrypt the stored API key) live in `App_Data` on a Railway volume at `/data`. To reset the demo, delete the files on the volume and redeploy.

**Accounts** (created on every start if missing; existing ones are never changed; Orchard Core's password rules: at least 6 characters with an upper-case letter, a lower-case letter, a digit and a symbol — a password that doesn't meet them skips the account with a warning in the log naming the variable):

| Variables | Account |
| --- | --- |
| `DEMO_ADMIN_EMAIL`, `DEMO_ADMIN_PASSWORD` (fallback `ORCHARD_ADMIN_*`) | *Administrator* role; also the AutoSetup admin on first boot (required then — the container exits without it, and an invalid password makes the setup fail; see the log) |
| `DEMO_EDITOR_EMAIL`, `DEMO_EDITOR_PASSWORD` | *Editor* role — can edit, translate and publish content in all cultures |

Orchard Core's setup screen never appears: AutoSetup installs the site.

**Service variables:** `DEMO_*` (above), `SUPERTEXT_API_KEY`, optional `SUPERTEXT_API_ENDPOINT`, `PORT=8080` (the domain's target port), `RAILWAY_DOCKERFILE_PATH=demo/Dockerfile`. On Railway the `DEMO_*` variables reference the umbraco service's (`${{umbraco.DEMO_ADMIN_EMAIL}}` …), so all .NET demos share one set.

**Run it locally:**

```bash
docker build -f demo/Dockerfile -t supertext-orchardcore-demo .
docker run --rm -p 8080:8080 -v orcharddemo:/data \
  -e DEMO_ADMIN_EMAIL=admin@example.com -e DEMO_ADMIN_PASSWORD='Choose-a-pass-1!' \
  -e SUPERTEXT_API_KEY=... supertext-orchardcore-demo
```

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
- Human (professional) translation orders are not supported yet.
- Tested on Orchard Core 3.0.1 with the stand-in API; not yet against the live API.
