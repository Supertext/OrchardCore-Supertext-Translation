# Supertext Translation for Orchard Core

Translate Orchard Core content items into other cultures with [Supertext](https://www.supertext.com) AI translation.

- **Translate with Supertext**: a button in the content list and the editor translates an item into the cultures you pick. Missing translations are created, existing ones are overwritten after you confirm. Every result is saved as a draft to review.
- **Automatic**: when you create a culture version with Orchard Core's own *Localizations* menu, the copy is translated before the editor opens it.
- Translates titles, HTML and Markdown bodies, text, HTML, Markdown and link fields, media alt texts, and widgets in Flow, Bag and Widgets List parts. Formatting, links and Liquid stay intact.

Requires Orchard Core 3.0 (.NET 10) with the *Content Localization* feature, and a Supertext API key: [create a Supertext account](https://www.supertext.com/person/en/account/signin), then generate the key at [supertext.com → Integrations → API](https://www.supertext.com/en/integrations/api) (requires the Admin role).

| Guide | For |
| --- | --- |
| [Installation](https://github.com/Supertext/OrchardCore-Supertext-Translation/blob/main/docs/INSTALLATION.md) | Administrators: install, API key, cultures, settings, troubleshooting |
| [User guide](https://github.com/Supertext/OrchardCore-Supertext-Translation/blob/main/docs/USER_GUIDE.md) | Editors: translating and reviewing content |
| [Developer guide](https://github.com/Supertext/OrchardCore-Supertext-Translation/blob/main/docs/DEVELOPER.md) | Architecture, Supertext API, tests, demo, releasing |

Demo: <https://orchardcore-production.up.railway.app/> (admin at `/Admin`).

![Translate with Supertext: German translated as a draft](https://raw.githubusercontent.com/Supertext/OrchardCore-Supertext-Translation/main/docs/images/translate-after.png)

Part of Supertext's translation plugins for the top open source CMS. Changes: [CHANGELOG.md](https://github.com/Supertext/OrchardCore-Supertext-Translation/blob/main/CHANGELOG.md).

<!-- supertext-plugins:start (shared list, keep identical in every Supertext plugin repo) -->
## Supertext plugins for other systems

Supertext offers AI and professional translation plugins for these systems:

| System | Plugin | What it does |
| --- | --- | --- |
| Adobe Experience Manager | [supertext-aem-connector](https://github.com/Supertext/supertext-aem-connector) | Translation connector for AEM 6.5's Translation Integration Framework |
| Contao | [Contao-Supertext-Translation](https://github.com/Supertext/Contao-Supertext-Translation) | *Translate with Supertext* in the site structure: pages or whole websites into other languages |
| Craft CMS | [CraftCms-Supertext-Translation](https://github.com/Supertext/CraftCms-Supertext-Translation) | Translates entries into your other sites, Matrix and rich text included |
| Directus | [Directus-Supertext-Translation](https://github.com/Supertext/Directus-Supertext-Translation) | *Translate with Supertext* box on the item form, fills the Translations field |
| django CMS | [djangoCMS-Supertext-Translation](https://github.com/Supertext/djangoCMS-Supertext-Translation) | Translates pages and their plugins from the toolbar |
| Drupal | [tmgmt_supertext_ai](https://www.drupal.org/project/tmgmt_supertext_ai) | Supertext AI provider for Drupal's Translation Management Tool (TMGMT), by MD Systems |
| Ghost | [Ghost-Supertext-Translation](https://github.com/Supertext/Ghost-Supertext-Translation) | Tag a post `#translate-…` and a translated draft appears |
| Grav | [Grav-Supertext-Translation](https://github.com/Supertext/Grav-Supertext-Translation) | Supertext panel in Grav 2's page editor, Markdown kept intact |
| Joomla | [Joomla-Supertext-Translation](https://github.com/Supertext/Joomla-Supertext-Translation) | Translates articles into linked, unpublished language versions |
| Neos | [Neos-Supertext-Translation](https://github.com/Supertext/Neos-Supertext-Translation) | Translates automatically when an editor creates a page in another language |
| Orchard Core | [OrchardCore-Supertext-Translation](https://github.com/Supertext/OrchardCore-Supertext-Translation) | Translates content items into other cultures, on demand or on localization |
| Payload CMS | [Payload-Supertext-Translation](https://github.com/Supertext/Payload-Supertext-Translation) | *Translate* button for localized collections and globals |
| Silverstripe | [Silverstripe-Supertext-Translation](https://github.com/Supertext/Silverstripe-Supertext-Translation) | Supertext tab translates pages and Elemental blocks into Fluent locales |
| Strapi | [Strapi-Supertext-Translation](https://github.com/Supertext/Strapi-Supertext-Translation) | Translates entries into other locales from the Content Manager |
| TYPO3 | [Typo3-Supertext-Translation](https://github.com/Supertext/Typo3-Supertext-Translation) | Translates pages and content elements as editors localize them |
| Umbraco | [Umbraco-Supertext-Translation](https://github.com/Supertext/Umbraco-Supertext-Translation) | *Translate with Supertext* for pages, block lists and grids included |
| Wagtail | [Wagtail-Supertext-Translation](https://github.com/Supertext/Wagtail-Supertext-Translation) | Machine translator for wagtail-localize |
| WordPress (Polylang) | [supertext-wordpress-polylang](https://github.com/Supertext/supertext-wordpress-polylang) | Supertext as Polylang Pro's machine-translation service, plus professional translation orders |
<!-- supertext-plugins:end -->
