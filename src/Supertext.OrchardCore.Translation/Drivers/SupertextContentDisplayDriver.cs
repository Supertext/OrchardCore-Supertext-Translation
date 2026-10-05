using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.ContentLocalization;
using OrchardCore.Contents;
using OrchardCore.ContentLocalization.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace Supertext.OrchardCore.Translation.Drivers;

/// <summary>Adds "Translate with Supertext" to the content list (next to Localizations) and the editor's action bar.</summary>
public sealed class SupertextContentDisplayDriver(IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor) : ContentDisplayDriver
{
    public override IDisplayResult Display(ContentItem contentItem, BuildDisplayContext context)
    {
        if (!contentItem.Has(nameof(LocalizationPart)))
        {
            return null;
        }
        return Shape("SupertextButton_SummaryAdmin", new ContentItemViewModel(contentItem))
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:6")
            .RenderWhen(() => CanTranslateAsync(contentItem));
    }

    public override IDisplayResult Edit(ContentItem contentItem, BuildEditorContext context)
    {
        // Only saved items can be translated (their saved version is what gets translated).
        if (contentItem.Id == 0 || !contentItem.Has(nameof(LocalizationPart)))
        {
            return null;
        }
        return Shape("SupertextButton_Edit", new ContentItemViewModel(contentItem))
            .Location("Actions:35")
            .RenderWhen(() => CanTranslateAsync(contentItem));
    }

    /// <summary>Same checks as the Translate page, so the button never leads to "access denied".</summary>
    private async Task<bool> CanTranslateAsync(ContentItem contentItem)
    {
        var user = httpContextAccessor.HttpContext?.User;
        return await authorizationService.AuthorizeAsync(user, SupertextPermissions.TranslateWithSupertext, contentItem)
            && await authorizationService.AuthorizeAsync(user, ContentLocalizationPermissions.LocalizeContent, contentItem)
            && await authorizationService.AuthorizeAsync(user, CommonPermissions.EditContent, contentItem);
    }
}
