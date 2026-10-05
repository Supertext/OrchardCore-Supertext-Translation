using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Settings;
using Supertext.OrchardCore.Translation.Api;
using Supertext.OrchardCore.Translation.Settings;

namespace Supertext.OrchardCore.Translation.Services;

/// <summary>
/// The settings in effect: site settings, overridden by the <c>SUPERTEXT_API_KEY</c> /
/// <c>SUPERTEXT_API_ENDPOINT</c> environment variables or the tenant's <c>Supertext</c>
/// configuration section (<c>Supertext:ApiKey</c>, <c>Supertext:Endpoint</c>).
/// </summary>
public sealed class SupertextConfiguration(
    ISiteService siteService,
    IDataProtectionProvider dataProtectionProvider,
    IShellConfiguration shellConfiguration,
    ILogger<SupertextConfiguration> logger)
{
    public const string ProtectorPurpose = "Supertext.OrchardCore.Translation.ApiKey";

    public async Task<SupertextSettings> GetSettingsAsync()
        => (await siteService.GetSiteSettingsAsync()).TryGet<SupertextSettings>(out var settings) ? settings : new SupertextSettings();

    public string ApiKeyFromEnvironment
        => FirstNonEmpty(Environment.GetEnvironmentVariable("SUPERTEXT_API_KEY"), shellConfiguration["Supertext:ApiKey"]);

    public string EndpointFromEnvironment
        => FirstNonEmpty(Environment.GetEnvironmentVariable("SUPERTEXT_API_ENDPOINT"), shellConfiguration["Supertext:Endpoint"]);

    public async Task<SupertextConnection> GetConnectionAsync()
    {
        var settings = await GetSettingsAsync();
        var apiKey = FirstNonEmpty(ApiKeyFromEnvironment, Unprotect(settings.ProtectedApiKey));
        var endpoint = FirstNonEmpty(EndpointFromEnvironment, settings.Endpoint, SupertextClient.DefaultEndpoint);
        return new SupertextConnection(endpoint, SupertextClient.NormalizeApiKey(apiKey), PollTimeoutSeconds: settings.PollTimeoutSeconds > 0 ? settings.PollTimeoutSeconds : 300);
    }

    public string Protect(string apiKey)
        => string.IsNullOrWhiteSpace(apiKey) ? null : dataProtectionProvider.CreateProtector(ProtectorPurpose).Protect(SupertextClient.NormalizeApiKey(apiKey));

    public string Unprotect(string protectedKey)
    {
        if (string.IsNullOrWhiteSpace(protectedKey))
        {
            return string.Empty;
        }
        try
        {
            return dataProtectionProvider.CreateProtector(ProtectorPurpose).Unprotect(protectedKey);
        }
        catch (Exception e)
        {
            // Happens when the data protection keys were lost (e.g. a new container without them).
            logger.LogWarning(e, "The stored Supertext API key could not be decrypted. Enter it again in Settings → Supertext.");
            return string.Empty;
        }
    }

    /// <summary>The language code sent to Supertext for an Orchard Core culture.</summary>
    public static string TargetLanguageCode(SupertextSettings settings, string culture)
    {
        foreach (var line in (settings.LanguageMapping ?? string.Empty).Split('\n'))
        {
            var pair = line.Split('=', 2);
            if (pair.Length == 2 && pair[0].Trim().Equals(culture, StringComparison.OrdinalIgnoreCase) && pair[1].Trim() != string.Empty)
            {
                return pair[1].Trim();
            }
        }
        return culture;
    }

    public static IEnumerable<string> ExcludedFields(SupertextSettings settings)
        => (settings.ExcludedFields ?? string.Empty).Split('\n', ',').Select(e => e.Trim()).Where(e => e != string.Empty);

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;
}
