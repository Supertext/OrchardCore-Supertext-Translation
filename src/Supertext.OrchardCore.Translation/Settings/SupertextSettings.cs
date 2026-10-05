namespace Supertext.OrchardCore.Translation.Settings;

/// <summary>Site settings (Settings → Supertext). Stored in the site document.</summary>
public sealed class SupertextSettings
{
    /// <summary>API key, encrypted with ASP.NET Core data protection.</summary>
    public string ProtectedApiKey { get; set; }

    /// <summary>Empty = https://api.supertext.com/v1/.</summary>
    public string Endpoint { get; set; }

    /// <summary>"default", "more" (formal) or "less" (informal).</summary>
    public string Politeness { get; set; } = "default";

    /// <summary>Translate the copy when an editor creates a localization with Orchard Core's own culture menu.</summary>
    public bool TranslateNewLocalizations { get; set; } = true;

    /// <summary>One "culture=supertext-code" per line, e.g. "de=de-CH". Cultures not listed are sent as they are.</summary>
    public string LanguageMapping { get; set; }

    /// <summary>One "Part.Field", "Part" or "ContentType.Part.Field" per line.</summary>
    public string ExcludedFields { get; set; }

    public int PollTimeoutSeconds { get; set; } = 300;
}
