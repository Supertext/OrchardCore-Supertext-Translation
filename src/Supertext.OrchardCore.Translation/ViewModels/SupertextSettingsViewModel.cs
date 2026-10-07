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

    public string LanguageMapping { get; set; }

    public string ExcludedFields { get; set; }

    public int PollTimeoutSeconds { get; set; }

    // Display only
    public bool HasStoredApiKey { get; set; }

    public bool ApiKeyFromEnvironment { get; set; }

    public string EndpointInEffect { get; set; }

    public bool EndpointFromEnvironment { get; set; }

    public IReadOnlyList<string> Cultures { get; set; } = [];

    public string ModuleVersion { get; set; }

    /// <summary>GitHub release page when <see cref="ModuleVersion"/> is a release (X.Y.Z), else null.</summary>
    public string ModuleReleaseUrl { get; set; }
}
