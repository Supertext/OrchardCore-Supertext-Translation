# Supertext Translation for Orchard Core

Translate Orchard Core content items into other cultures with [Supertext](https://www.supertext.com) AI translation.

- **Translate with Supertext**: a button in the content list and the editor translates an item into the cultures you pick. Missing translations are created, existing ones are overwritten after you confirm. Every result is saved as a draft to review.
- **Automatic**: when you create a culture version with Orchard Core's own *Localizations* menu, the copy is translated before the editor opens it.
- Translates titles, HTML and Markdown bodies, text, HTML, Markdown and link fields, media alt texts, and widgets in Flow, Bag and Widgets List parts. Formatting, links and Liquid stay intact.

Requires Orchard Core 3.0 (.NET 10) with the *Content Localization* feature.

| Guide | For |
| --- | --- |
| [Installation](https://github.com/Supertext/OrchardCore-Supertext-Translation/blob/main/docs/INSTALLATION.md) | Administrators: install, API key, cultures, settings, troubleshooting |
| [User guide](https://github.com/Supertext/OrchardCore-Supertext-Translation/blob/main/docs/USER_GUIDE.md) | Editors: translating and reviewing content |
| [Developer guide](https://github.com/Supertext/OrchardCore-Supertext-Translation/blob/main/docs/DEVELOPER.md) | Architecture, Supertext API, tests, demo, releasing |

Demo: <https://orchardcore-production.up.railway.app/> (admin at `/Admin`).

![Translate with Supertext: German translated as a draft](https://raw.githubusercontent.com/Supertext/OrchardCore-Supertext-Translation/main/docs/images/translate-after.png)

Part of Supertext's translation plugins for the top open source CMS. Changes: [CHANGELOG.md](https://github.com/Supertext/OrchardCore-Supertext-Translation/blob/main/CHANGELOG.md).
