namespace Supertext.OrchardCore.Translation.ViewModels;

// Not sealed: Initialize<T>() creates a proxy of the view model.
public class SupertextSettingsViewModel
{
    /// <summary>New key; empty keeps the stored one.</summary>
    public string ApiKey { get; set; }

    public bool RemoveApiKey { get; set; }

    public string Endpoint { get; set; }

    public string Politeness { get; set; }

    public bool TranslateNewLocalizations { get; set; }

    /// <summary>Language mapping: one row per site culture (posted: Culture and Code).</summary>
    public List<LanguageMappingRow> Mappings { get; set; } = [];

    public bool SendVersionInformation { get; set; }

    public string ExcludedFields { get; set; }

    public int PollTimeoutSeconds { get; set; }

    // Display only
    public bool HasStoredApiKey { get; set; }

    public bool ApiKeyFromEnvironment { get; set; }

    public string EndpointInEffect { get; set; }

    public bool EndpointFromEnvironment { get; set; }

    /// <summary>What SUPERTEXT_VERSION_REPORTS forces (the checkbox is then read-only), or null.</summary>
    public bool? VersionReportsFromEnvironment { get; set; }

    /// <summary>The latest automatic API key check (SDK result), or null if none has run yet.</summary>
    public Sdk.V1.ApiKeyCheck KeyCheck { get; set; }

    /// <summary>Supertext's target languages for the mapping selects.</summary>
    public IReadOnlyList<Sdk.V1.TargetLanguage> TargetLanguages { get; set; } = [];

    public string ModuleVersion { get; set; }

    /// <summary>GitHub release page when <see cref="ModuleVersion"/> is a release (X.Y.Z), else null.</summary>
    public string ModuleReleaseUrl { get; set; }
}

// Not sealed: part of the proxied view model.
public class LanguageMappingRow
{
    public string Culture { get; set; }

    /// <summary>Supertext code; empty = not mapped.</summary>
    public string Code { get; set; }

    // Display only
    public string CultureName { get; set; }

    /// <summary>The selected code isn't in Supertext's list (e.g. es-ES from an older mapping).</summary>
    public bool UnknownCode { get; set; }

    /// <summary>The form of address is set, but Supertext offers no formal/informal choice for this language.</summary>
    public bool PolitenessUnavailable { get; set; }
}
