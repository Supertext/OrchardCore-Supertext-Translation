using System.Reflection;
using System.Text.RegularExpressions;

namespace Supertext.OrchardCore.Translation.Services;

/// <summary>
/// The module's version, read from the assembly (set by <c>&lt;Version&gt;</c> in the .csproj),
/// so there is no second copy to keep in sync.
/// </summary>
public static partial class ModuleVersion
{
    public const string ReleasesUrl = "https://github.com/Supertext/OrchardCore-Supertext-Translation/releases/tag/v";

    /// <summary>The version without build metadata, e.g. "0.1.0".</summary>
    public static string Current { get; } = Normalize(
        typeof(ModuleVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(ModuleVersion).Assembly.GetName().Version?.ToString(3));

    /// <summary>Strips the "+commit" build metadata the SDK appends; empty for null.</summary>
    public static string Normalize(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }
        var plus = version.IndexOf('+');
        return (plus >= 0 ? version[..plus] : version).Trim();
    }

    /// <summary>The GitHub release page for a release version (X.Y.Z), otherwise null.</summary>
    public static string ReleaseUrl(string version)
        => version is not null && ReleasePattern().IsMatch(version) ? ReleasesUrl + version : null;

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex ReleasePattern();
}
