using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentLocalization.Handlers;
using OrchardCore.ContentLocalization.Models;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Notify;
using Supertext.OrchardCore.Translation.Api;
using Supertext.OrchardCore.Translation.Services;

namespace Supertext.OrchardCore.Translation.Handlers;

/// <summary>
/// Hooks into Orchard Core's own "create localization" step (the culture menu in the
/// content list and editor): the copy Orchard Core makes of the source is translated before
/// the editor opens it. Failures never block the localization; the editor gets an
/// untranslated copy and a warning.
/// </summary>
public sealed class SupertextLocalizationHandler(
    ContentTranslator translator,
    SupertextConfiguration configuration,
    SupertextScope scope,
    INotifier notifier,
    SupertextMessages messages,
    IHtmlLocalizer<SupertextLocalizationHandler> htmlLocalizer,
    ILogger<SupertextLocalizationHandler> logger) : ContentLocalizationHandlerBase
{
    private readonly IHtmlLocalizer H = htmlLocalizer;

    public override async Task LocalizingAsync(LocalizationContentContext context)
    {
        if (scope.SuppressAutomaticTranslation || !(await configuration.GetSettingsAsync()).TranslateNewLocalizations)
        {
            return;
        }

        var sourceCulture = context.Original.Localization()?.Culture ?? string.Empty;
        var cultureName = CultureNames.Get(context.Culture);
        try
        {
            var result = await translator.TranslateAsync(context.Original, context.ContentItem, sourceCulture, context.Culture);
            if (result.Segments == 0)
            {
                return;
            }
            if (result.Untranslated > 0)
            {
                await notifier.WarningAsync(H["Translated into {0} with Supertext. {1} text(s) kept the source wording, please review them.", cultureName, result.Untranslated]);
            }
            else
            {
                await notifier.SuccessAsync(H["Translated into {0} with Supertext. Please review the translation before publishing.", cultureName]);
            }
        }
        catch (SupertextException e)
        {
            logger.LogWarning(e, "Supertext translation of {ContentItemId} into {Culture} failed.", context.Original.ContentItemId, context.Culture);
            await notifier.WarningAsync(H["The {0} version was created but not translated: {1}", cultureName, messages.Describe(e)]);
        }
        catch (Exception e)
        {
            // Never break Orchard Core's own localization: the editor still gets the copy.
            logger.LogError(e, "Unexpected error translating {ContentItemId} into {Culture}.", context.Original.ContentItemId, context.Culture);
            await notifier.WarningAsync(H["The {0} version was created but not translated because of an unexpected error. Try Translate with Supertext later.", cultureName]);
        }
    }
}
