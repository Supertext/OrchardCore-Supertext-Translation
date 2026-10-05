namespace Supertext.OrchardCore.Translation.Services;

/// <summary>Per-request state: the Translate page creates localizations itself and translates them explicitly.</summary>
public sealed class SupertextScope
{
    public bool SuppressAutomaticTranslation { get; set; }
}
