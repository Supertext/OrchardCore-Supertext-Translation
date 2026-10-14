using Supertext.OrchardCore.Translation.Sdk.V1;

namespace Supertext.OrchardCore.Translation.Api;

/// <summary>What the reporter is asked to do (Commando Center → <i>When a plugin reports</i>).</summary>
public enum ReporterAction
{
    /// <summary>Settings → Supertext was saved: check the key, report <c>installed</c> or <c>settings_saved</c>.</summary>
    AfterSettingsSaved,

    /// <summary>A back-end request ran the module's code: check a changed key, report what changed.</summary>
    OnRequest,

    /// <summary>A translation was rejected with 401/403.</summary>
    AuthenticationFailed,
}

/// <summary>What the reporter needs to know about the installation.</summary>
/// <param name="Enabled">The setting combined with <c>SUPERTEXT_VERSION_REPORTS</c> (<see cref="VersionReports.Enabled"/>).</param>
/// <param name="SiteUrl">The site's base URL (<see cref="VersionReports.SiteUrl"/>).</param>
/// <param name="ModuleVersion">From the assembly (<c>ModuleVersion.Current</c>).</param>
/// <param name="OrchardVersion">The running Orchard Core version.</param>
/// <param name="Endpoint">The Supertext API endpoint in effect (for the key check).</param>
public sealed record ReportingContext(bool Enabled, string SiteUrl, string ModuleVersion, string OrchardVersion, string Endpoint)
{
    /// <summary>For tests: the report URL (else <c>SUPERTEXT_REPORT_URL</c> or the live center).</summary>
    public string ReportUrl { get; init; }

    /// <summary>For tests: the HttpClient for the key check and the report.</summary>
    public HttpClient HttpClient { get; init; }

    /// <summary>For tests: the clock.</summary>
    public TimeProvider TimeProvider { get; init; }
}

/// <summary>
/// The module's side of the Supertext Commando Center: version reports, the automatic API key
/// check and the callback ping. The protocol is the SDK's (<see cref="InstallationReporter"/>,
/// <see cref="PingResponse"/>); this class only supplies what Orchard Core knows. No Orchard Core
/// dependency (unit-tested); <c>Services/SupertextReporting</c> stores the state and runs it.
/// </summary>
public static class VersionReports
{
    /// <summary>The GitHub repository name, sent as <c>plugin.name</c> and in the ping answer.</summary>
    public const string PluginName = "OrchardCore-Supertext-Translation";

    /// <summary><c>system.name</c>, in Orchard Core's own spelling.</summary>
    public const string SystemName = "Orchard Core";

    /// <summary>The public ping route below the site's base URL.</summary>
    public const string PingPath = "supertext/ping";

    /// <summary>Whether reports are on: <c>SUPERTEXT_VERSION_REPORTS</c> (0/false/off/no, 1/true/on/yes) wins over the setting.</summary>
    public static bool Enabled(bool setting) => Reporting.ReportsEnabled(setting);

    /// <summary>Like <see cref="Enabled(bool)"/>, with the environment value passed in (tests).</summary>
    public static bool Enabled(bool setting, string environment) => Reporting.ReportsEnabled(setting, environment);

    /// <summary>What <c>SUPERTEXT_VERSION_REPORTS</c> forces (true/false), or null when it isn't set or not a known value.</summary>
    public static bool? EnvironmentOverride(string environment = null)
    {
        environment ??= Environment.GetEnvironmentVariable("SUPERTEXT_VERSION_REPORTS");
        return Reporting.ReportsEnabled(false, environment) ? true
            : Reporting.ReportsEnabled(true, environment) ? null
            : false;
    }

    /// <summary>
    /// The site's base URL: Orchard Core's <b>Base URL</b> setting (Settings → General) when it is an
    /// absolute http(s) URL, else the address of the request (scheme, host, path base of the tenant).
    /// No trailing slash; null if neither is known.
    /// </summary>
    public static string SiteUrl(string configuredBaseUrl, string scheme, string host, string pathBase)
    {
        var configured = (configuredBaseUrl ?? string.Empty).Trim();
        if (Uri.TryCreate(configured, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return configured.TrimEnd('/');
        }
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }
        var safeScheme = string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase) ? "https" : "http";
        return (safeScheme + "://" + host + (pathBase ?? string.Empty)).TrimEnd('/');
    }

    /// <summary>The absolute URL of the ping route for a site URL.</summary>
    public static string CallbackUrl(string siteUrl) => siteUrl.TrimEnd('/') + "/" + PingPath;

    /// <summary>The ping route's answer: status, headers and body exactly as the SDK returns them (404 while reports are off).</summary>
    public static PingResponse Ping(string challenge, bool enabled) => PingResponse.For(challenge, PluginName, enabled);

    /// <summary>The SDK reporter for this installation.</summary>
    public static InstallationReporter CreateReporter(ReportingContext context)
    {
        var clientOptions = SupertextClient.KeyCheckOptions(context.Endpoint);
        if (context.HttpClient is not null)
        {
            clientOptions = new Sdk.V1.SupertextClientOptions
            {
                BaseUrl = clientOptions.BaseUrl,
                UserAgent = clientOptions.UserAgent,
                HttpClient = context.HttpClient,
            };
        }
        return new InstallationReporter(new ReporterOptions
        {
            Enabled = context.Enabled,
            SiteUrl = context.SiteUrl,
            Plugin = new NameVersion(PluginName, string.IsNullOrEmpty(context.ModuleVersion) ? "0.0.0" : context.ModuleVersion),
            System = new NameVersion(SystemName, string.IsNullOrEmpty(context.OrchardVersion) ? null : context.OrchardVersion),
            CallbackUrl = CallbackUrl(context.SiteUrl),
            ReportUrl = context.ReportUrl,
            ClientOptions = clientOptions,
            HttpClient = context.HttpClient,
            TimeProvider = context.TimeProvider,
        });
    }

    /// <summary>Runs one reporter action on the stored state. Never throws: on any error the state is returned unchanged with no event.</summary>
    /// <param name="apiKey">The key in effect (it never leaves the SDK; only its fingerprint is kept).</param>
    /// <param name="httpStatus">For <see cref="ReporterAction.AuthenticationFailed"/>: 401 or 403.</param>
    public static async Task<ReporterResult> RunAsync(ReporterAction action, ReportingContext context, ReporterState state, string apiKey, int httpStatus = 401)
    {
        state ??= ReporterState.Empty;
        try
        {
            var reporter = CreateReporter(context);
            return action switch
            {
                ReporterAction.AfterSettingsSaved => await reporter.AfterSettingsSavedAsync(state, apiKey),
                ReporterAction.AuthenticationFailed => await reporter.AuthenticationFailedAsync(state, apiKey, httpStatus is 401 or 403 ? httpStatus : 401),
                _ => await reporter.OnRequestAsync(state, apiKey),
            };
        }
        catch (Exception)
        {
            return new ReporterResult(state, state.KeyCheck, null, false);
        }
    }
}
