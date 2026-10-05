using OrchardCore.ContentLocalization.Models;
using OrchardCore.ContentManagement;

namespace Supertext.OrchardCore.Translation.Services;

public static class ContentItemLocalizationExtensions
{
    /// <summary>The item's LocalizationPart, or null.</summary>
    public static LocalizationPart Localization(this ContentItem item)
        => item.TryGet<LocalizationPart>(out var part) ? part : null;
}
