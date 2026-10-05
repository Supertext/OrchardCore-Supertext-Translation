namespace Supertext.OrchardCore.Translation.ViewModels;

public sealed class TranslateViewModel
{
    public string ContentItemId { get; set; }

    public string Title { get; set; }

    public string ContentType { get; set; }

    public string SourceCulture { get; set; }

    public string SourceCultureName { get; set; }

    public bool HasDraft { get; set; }

    public bool ApiKeyMissing { get; set; }

    public bool ConfirmOverwrite { get; set; }

    public bool ShowOverwriteWarning { get; set; }

    public string ReturnUrl { get; set; }

    public List<TranslateTarget> Targets { get; set; } = [];
}

public sealed class TranslateTarget
{
    public string Culture { get; set; }

    public string CultureName { get; set; }

    public bool Selected { get; set; }

    /// <summary>Content item of the existing localization, null if there is none yet.</summary>
    public string ExistingContentItemId { get; set; }

    public string ExistingTitle { get; set; }

    public bool ExistingPublished { get; set; }

    public bool ExistingHasDraft { get; set; }
}
