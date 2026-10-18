using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;
using Supertext.OrchardCore.Translation.Api;
using KeyCheckStatus = Supertext.OrchardCore.Translation.PluginHealth.KeyCheckStatus;
using Supertext.OrchardCore.Translation.Services;
using Supertext.OrchardCore.Translation.Settings;
using Supertext.OrchardCore.Translation.ViewModels;

namespace Supertext.OrchardCore.Translation.Drivers;

/// <summary>
/// Settings → Supertext. Saving runs the automatic API key check and the version report
/// (<see cref="SupertextReporting.AfterSettingsSavedAsync"/>); opening the page runs
/// <see cref="SupertextReporting.OnRequestAsync"/> after the response.
/// </summary>
public sealed class SupertextSettingsDisplayDriver(
    SupertextConfiguration configuration,
    SupertextReporting reporting,
    KeyCheckMessages keyCheckMessages,
    ILocalizationService localizationService,
    INotifier notifier,
    IHttpContextAccessor httpContextAccessor,
    IAuthorizationService authorizationService,
    IHtmlLocalizer<SupertextSettingsDisplayDriver> htmlLocalizer,
    IStringLocalizer<SupertextSettingsDisplayDriver> stringLocalizer) : SiteDisplayDriver<SupertextSettings>
{
    public const string GroupId = "supertext";

    private readonly IHtmlLocalizer H = htmlLocalizer;
    private readonly IStringLocalizer S = stringLocalizer;

    protected override string SettingsGroupId => GroupId;

    public override async Task<IDisplayResult> EditAsync(ISite site, SupertextSettings settings, BuildEditorContext context)
    {
        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext?.User, SupertextPermissions.ManageSupertextSettings))
        {
            return null;
        }

        var cultures = await localizationService.GetSupportedCulturesAsync();
        var environmentEndpoint = configuration.EndpointFromEnvironment;
        var mapping = SupertextConfiguration.Mapping(settings);
        var keyCheck = SupertextReporting.LoadState(site).KeyCheck;
        if (HttpMethods.IsGet(httpContextAccessor.HttpContext?.Request.Method ?? string.Empty))
        {
            await reporting.OnRequestAsync();
        }

        return Initialize<SupertextSettingsViewModel>("SupertextSettings_Edit", model =>
        {
            model.HasStoredApiKey = !string.IsNullOrEmpty(settings.ProtectedApiKey);
            model.ApiKeyFromEnvironment = configuration.ApiKeyFromEnvironment != string.Empty;
            model.Endpoint = settings.Endpoint;
            model.EndpointFromEnvironment = environmentEndpoint != string.Empty;
            model.EndpointInEffect = environmentEndpoint != string.Empty ? environmentEndpoint
                : string.IsNullOrWhiteSpace(settings.Endpoint) ? SupertextClient.DefaultEndpoint : settings.Endpoint;
            model.Politeness = settings.Politeness ?? "default";
            model.TranslateNewLocalizations = settings.TranslateNewLocalizations;
            model.Mappings = cultures.Select(culture => MappingRow(culture, mapping, settings.Politeness)).ToList();
            model.TargetLanguages = LanguageMapping.Targets;
            model.ExcludedFields = settings.ExcludedFields;
            model.PollTimeoutSeconds = settings.PollTimeoutSeconds;
            model.SendVersionInformation = settings.SendVersionInformation;
            model.VersionReportsFromEnvironment = VersionReports.EnvironmentOverride();
            model.KeyCheck = keyCheck;
            model.ModuleVersion = ModuleVersion.Current;
            model.ModuleReleaseUrl = ModuleVersion.ReleaseUrl(ModuleVersion.Current);
        }).Location("Content:2").OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, SupertextSettings settings, UpdateEditorContext context)
    {
        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext?.User, SupertextPermissions.ManageSupertextSettings))
        {
            return null;
        }

        var model = new SupertextSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var endpointFromEnvironment = configuration.EndpointFromEnvironment != string.Empty;
        if (!endpointFromEnvironment && !string.IsNullOrWhiteSpace(model.Endpoint) && !IsSafeEndpoint(model.Endpoint.Trim()))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["The endpoint must be an https URL, e.g. https://api.supertext.com/v1/ (http only for localhost)."]);
        }
        if (model.PollTimeoutSeconds is < 30 or > 3600)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PollTimeoutSeconds), S["The timeout must be between 30 and 3600 seconds."]);
        }

        if (context.Updater.ModelState.IsValid)
        {
            if (model.RemoveApiKey)
            {
                settings.ProtectedApiKey = null;
            }
            else if (!string.IsNullOrWhiteSpace(model.ApiKey))
            {
                settings.ProtectedApiKey = configuration.Protect(model.ApiKey);
            }
            // A disabled field (endpoint set by the environment) isn't posted: keep the stored value.
            if (!endpointFromEnvironment)
            {
                settings.Endpoint = string.IsNullOrWhiteSpace(model.Endpoint) ? null : model.Endpoint.Trim();
            }
            settings.Politeness = model.Politeness is "more" or "less" ? model.Politeness : "default";
            settings.TranslateNewLocalizations = model.TranslateNewLocalizations;
            settings.LanguageMapping = MergeMapping(settings.LanguageMapping, model.Mappings);
            settings.ExcludedFields = model.ExcludedFields?.Trim();
            settings.PollTimeoutSeconds = model.PollTimeoutSeconds;
            // A disabled checkbox (SUPERTEXT_VERSION_REPORTS set) isn't posted: keep the stored value.
            if (VersionReports.EnvironmentOverride() is null)
            {
                settings.SendVersionInformation = model.SendVersionInformation;
            }

            // Automatic API key check and version report; never blocks saving.
            var result = await reporting.AfterSettingsSavedAsync(site, settings);
            if (result.Check is { } check)
            {
                var text = keyCheckMessages.Describe(check);
                await (check.Status switch
                {
                    KeyCheckStatus.Ok => notifier.SuccessAsync(text),
                    KeyCheckStatus.Invalid => notifier.ErrorAsync(text),
                    _ => notifier.WarningAsync(text),
                });
            }
        }

        return await EditAsync(site, settings, context);
    }

    /// <summary>The API key travels in a header: only https, or http to this machine for testing.</summary>
    private static bool IsSafeEndpoint(string endpoint)
        => Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));

    /// <summary>A row of the Language mapping table: the stored code, else the suggested Supertext code (the exact match, else the SDK's default target).</summary>
    private static LanguageMappingRow MappingRow(string culture, IReadOnlyDictionary<string, string> mapping, string politeness)
    {
        var code = mapping.TryGetValue(culture, out var mapped) ? mapped : LanguageMapping.SuggestTarget(culture) ?? string.Empty;
        return new LanguageMappingRow
        {
            Culture = culture,
            CultureName = CultureNames.Get(culture),
            Code = code,
            UnknownCode = code != string.Empty && LanguageMapping.MatchTarget(code) is null,
            PolitenessUnavailable = code != string.Empty && politeness is "more" or "less" && LanguageMapping.SupportsPoliteness(code) == false,
        };
    }

    /// <summary>The posted rows over the stored mapping; entries for cultures the site no longer has are kept.</summary>
    private static string MergeMapping(string stored, IEnumerable<LanguageMappingRow> rows)
    {
        var mapping = new Dictionary<string, string>(LanguageMapping.Parse(stored), StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows ?? [])
        {
            if (string.IsNullOrWhiteSpace(row?.Culture))
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(row.Code))
            {
                mapping.Remove(row.Culture.Trim());
            }
            else
            {
                mapping[row.Culture.Trim()] = row.Code.Trim();
            }
        }
        return LanguageMapping.Serialize(mapping);
    }
}
