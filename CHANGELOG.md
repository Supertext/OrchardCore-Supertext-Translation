# Changelog

All notable changes to this project are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## Unreleased

### Added

- Its own message for an unsupported language pair, naming the codes ("Supertext does not support this language pair (en → xx-YY)"), in English, German, French and Italian.
- French and Italian interface (and German where it was missing): buttons, the Translate with Supertext page, Settings → Supertext, notifications and Supertext error messages follow the admin's interface language (`Localization/de.po`, `fr.po`, `it.po`). The "Authentication failed" and missing-key errors now also link to Supertext account signup and API key generation.
- Settings → Supertext shows the installed module version, linked to its GitHub release notes.

### Changed

- All Supertext API calls now go through the official Supertext .NET SDK, compiled into the module (nothing extra to install). The HtmlAgilityPack dependency is gone.

### Fixed

- Cultures without a region (e.g. `de`, `fr`) are now sent to Supertext as `de-DE`, `fr-FR` (Supertext rejects targets without region); **Language codes** mappings are normalized (`de_ch` → `de-CH`) and, for the source culture, sent without region.
- Locale variants such as `de_DE_formal` (in the **Language codes** mapping) are sent to Supertext as `de-DE`.

## 0.1.0 — 2026-10-07

### Added

- *Translate with Supertext* page (content list and editor buttons): translate an item into several cultures, create missing localizations, overwrite existing ones after confirmation; results are saved as drafts.
- Automatic translation of new localizations created with Orchard Core's *Localizations* menu (can be turned off).
- Settings → Supertext: encrypted API key (with connection check), endpoint (https only, except localhost), form of address, language code mapping, excluded fields, timeout. `SUPERTEXT_API_KEY` / `SUPERTEXT_API_ENDPOINT` override the settings.
- Permissions *Translate content with Supertext* (Administrator, Editor) and *Manage Supertext settings* (Administrator).
- Translates TitlePart, HtmlBodyPart, MarkdownBodyPart, TextField (prose editors), HtmlField, MarkdownField, LinkField text, MediaField alt texts, and widgets in FlowPart, BagPart and WidgetsListPart; Liquid in HTML is protected.
- Settings → Supertext and the "no API key" warning link to Supertext account signup and API key generation (Integrations → API, Admin role); same links in the installation guide, README and demo `.env.example`.
- Railway demo (Orchard Core 3.0.1, TheBlogTheme, English → German, French, Italian) with demo accounts from `DEMO_*` variables.
