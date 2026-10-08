using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using OrchardCore.ContentLocalization;
using OrchardCore.ContentLocalization.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Localization;
using Supertext.OrchardCore.Translation.Api;
using Supertext.OrchardCore.Translation.Services;
using Supertext.OrchardCore.Translation.ViewModels;

namespace Supertext.OrchardCore.Translation.Controllers;

/// <summary>
/// "Translate with Supertext": translates a content item into the cultures the editor picks.
/// Missing localizations are created (as drafts); existing ones are overwritten after the
/// editor confirms it.
/// </summary>
[Admin]
public sealed class AdminController(
    IContentManager contentManager,
    IContentLocalizationManager localizationManager,
    ILocalizationService localizationService,
    IAuthorizationService authorizationService,
    ContentTranslator translator,
    SupertextConfiguration configuration,
    SupertextScope scope,
    INotifier notifier,
    SupertextMessages messages,
    IHtmlLocalizer<AdminController> htmlLocalizer,
    ILogger<AdminController> logger) : Controller
{
    private readonly IHtmlLocalizer H = htmlLocalizer;

    [HttpGet]
    [Admin("Supertext/Translate/{contentItemId}", "Supertext.Translate")]
    public async Task<IActionResult> Translate(string contentItemId, string returnUrl = null)
    {
        var source = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (source is null)
        {
            return NotFound();
        }
        if (!await CanTranslateAsync(source))
        {
            return Forbid();
        }
        return View(await BuildModelAsync(source, returnUrl, []));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("Supertext/Translate/{contentItemId}", "Supertext.Translate.Post")]
    public async Task<IActionResult> Translate(string contentItemId, string[] cultures, bool confirmOverwrite, string returnUrl = null)
    {
        var source = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        if (source is null)
        {
            return NotFound();
        }
        if (!await CanTranslateAsync(source))
        {
            return Forbid();
        }

        var model = await BuildModelAsync(source, returnUrl, cultures ?? []);
        var selected = model.Targets.Where(t => t.Selected).ToList();
        if (selected.Count == 0)
        {
            await notifier.WarningAsync(H["Choose at least one language."]);
            return View(model);
        }
        if (model.ApiKeyMissing)
        {
            return View(model);
        }
        if (selected.Any(t => t.ExistingContentItemId is not null) && !confirmOverwrite)
        {
            model.ShowOverwriteWarning = true;
            return View(model);
        }

        // Localizations created here are translated explicitly below.
        scope.SuppressAutomaticTranslation = true;

        // One language after the other: Supertext limits requests per second per key.
        foreach (var target in selected)
        {
            try
            {
                var existing = target.ExistingContentItemId is null ? null : await contentManager.GetAsync(target.ExistingContentItemId, VersionOptions.Latest);
                if (existing is not null && !await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, existing))
                {
                    await notifier.ErrorAsync(H["{0}: you may not edit the existing translation.", target.CultureName]);
                    continue;
                }

                var prepared = await translator.PrepareAsync(source, model.SourceCulture, target.Culture, HttpContext.RequestAborted);

                var item = existing is null
                    ? await localizationManager.LocalizeAsync(source, target.Culture)
                    : await contentManager.GetAsync(existing.ContentItemId, VersionOptions.DraftRequired);
                ContentTranslator.Apply(prepared, item);
                await contentManager.UpdateAsync(item);
                await contentManager.SaveDraftAsync(item);

                if (prepared.Result.Untranslated > 0)
                {
                    await notifier.WarningAsync(H["{0}: translated as a draft, but {1} text(s) kept the source wording. Please review them.", target.CultureName, prepared.Result.Untranslated]);
                }
                else
                {
                    await notifier.SuccessAsync(H["{0}: translated as a draft ({1} texts). Review and publish it when it's ready.", target.CultureName, prepared.Result.Segments]);
                }
            }
            catch (SupertextException e)
            {
                logger.LogWarning(e, "Supertext translation of {ContentItemId} into {Culture} failed.", source.ContentItemId, target.Culture);
                await notifier.ErrorAsync(H["{0}: not translated. {1}", target.CultureName, messages.Describe(e)]);
            }
            catch (InvalidOperationException e)
            {
                logger.LogWarning(e, "Could not create the {Culture} localization of {ContentItemId}.", target.Culture, source.ContentItemId);
                await notifier.ErrorAsync(H["{0}: the localization could not be created.", target.CultureName]);
            }
        }

        return RedirectToAction(nameof(Translate), new { contentItemId, returnUrl });
    }

    private async Task<bool> CanTranslateAsync(ContentItem item)
        => await authorizationService.AuthorizeAsync(User, SupertextPermissions.TranslateWithSupertext, item)
            && await authorizationService.AuthorizeAsync(User, ContentLocalizationPermissions.LocalizeContent, item)
            && await authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item);

    private async Task<TranslateViewModel> BuildModelAsync(ContentItem source, string returnUrl, string[] selected)
    {
        var part = source.Localization();
        var sourceCulture = !string.IsNullOrEmpty(part?.Culture) ? part.Culture : await localizationService.GetDefaultCultureAsync();
        var existing = string.IsNullOrEmpty(part?.LocalizationSet) ? [] : (await localizationManager.GetItemsForSetAsync(part.LocalizationSet)).ToList();

        var model = new TranslateViewModel
        {
            ContentItemId = source.ContentItemId,
            Title = source.DisplayText,
            ContentType = source.ContentType,
            SourceCulture = sourceCulture,
            SourceCultureName = CultureName(sourceCulture),
            HasDraft = source.HasDraft(),
            ApiKeyMissing = (await configuration.GetConnectionAsync()).ApiKey == string.Empty,
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null,
        };

        foreach (var culture in await localizationService.GetSupportedCulturesAsync())
        {
            if (culture.Equals(sourceCulture, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            // Prefer the latest version (a draft) over the published one.
            var localized = existing
                .Where(i => string.Equals(i.Localization()?.Culture, culture, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(i => i.Latest)
                .FirstOrDefault();
            model.Targets.Add(new TranslateTarget
            {
                Culture = culture,
                CultureName = CultureName(culture),
                Selected = selected.Contains(culture, StringComparer.OrdinalIgnoreCase),
                ExistingContentItemId = localized?.ContentItemId,
                ExistingTitle = localized?.DisplayText,
                ExistingPublished = localized is not null && await contentManager.HasPublishedVersionAsync(localized),
                ExistingHasDraft = localized?.HasDraft() ?? false,
            });
        }
        return model;
    }

    private static string CultureName(string culture) => CultureNames.Get(culture);
}
