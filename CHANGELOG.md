# Changelog

All notable changes to this project are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## Unreleased

### Added

- *Translate with Supertext* page (content list and editor buttons): translate an item into several cultures, create missing localizations, overwrite existing ones after confirmation; results are saved as drafts.
- Automatic translation of new localizations created with Orchard Core's *Localizations* menu (can be turned off).
- Settings → Supertext: encrypted API key (with connection check), endpoint (https only, except localhost), form of address, language code mapping, excluded fields, timeout. `SUPERTEXT_API_KEY` / `SUPERTEXT_API_ENDPOINT` override the settings.
- Permissions *Translate content with Supertext* (Administrator, Editor) and *Manage Supertext settings* (Administrator).
- Translates TitlePart, HtmlBodyPart, MarkdownBodyPart, TextField (prose editors), HtmlField, MarkdownField, LinkField text, MediaField alt texts, and widgets in FlowPart, BagPart and WidgetsListPart; Liquid in HTML is protected.
- Settings → Supertext and the "no API key" warning link to Supertext account signup and API key generation (Integrations → API, Admin role); same links in the installation guide, README and demo `.env.example`.
- Railway demo (Orchard Core 3.0.1, TheBlogTheme, English → German, French, Italian) with demo accounts from `DEMO_*` variables.
