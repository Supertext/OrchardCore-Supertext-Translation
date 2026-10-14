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

    /// <summary>
    /// Language mapping: one "culture=supertext-code" per line, e.g. "de=de-CH". A culture that isn't
    /// listed is used only if it is a Supertext code itself (de-CH, es); otherwise it can't be translated.
    /// </summary>
    public string LanguageMapping { get; set; }

    /// <summary>One "Part.Field", "Part" or "ContentType.Part.Field" per line.</summary>
    public string ExcludedFields { get; set; }

    public int PollTimeoutSeconds { get; set; } = 300;

    /// <summary>"Send version information to Supertext" (on by default; SUPERTEXT_VERSION_REPORTS=0 overrides it).</summary>
    public bool SendVersionInformation { get; set; } = true;
}

/// <summary>
/// The Commando Center reporter's state (what was reported last, the latest API key check), stored
/// in the site document next to <see cref="SupertextSettings"/>: the SDK's <c>ReporterState</c> as JSON.
/// Never contains the API key (only a SHA-256 fingerprint to notice a changed key).
/// </summary>
public sealed class SupertextReportingState
{
    public string Reporter { get; set; }
}
