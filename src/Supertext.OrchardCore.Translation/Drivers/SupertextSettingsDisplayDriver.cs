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
using Supertext.OrchardCore.Translation.Services;
using Supertext.OrchardCore.Translation.Settings;
using Supertext.OrchardCore.Translation.ViewModels;

namespace Supertext.OrchardCore.Translation.Drivers;

/// <summary>Settings → Supertext.</summary>
public sealed class SupertextSettingsDisplayDriver(
    SupertextConfiguration configuration,
    SupertextClient client,
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
            model.LanguageMapping = settings.LanguageMapping;
            model.ExcludedFields = settings.ExcludedFields;
            model.PollTimeoutSeconds = settings.PollTimeoutSeconds;
            model.Cultures = cultures;
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
            settings.LanguageMapping = model.LanguageMapping?.Trim();
            settings.ExcludedFields = model.ExcludedFields?.Trim();
            settings.PollTimeoutSeconds = model.PollTimeoutSeconds;

            await CheckConnectionAsync(settings);
        }

        return await EditAsync(site, settings, context);
    }

    /// <summary>The API key travels in a header: only https, or http to this machine for testing.</summary>
    private static bool IsSafeEndpoint(string endpoint)
        => Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));

    /// <summary>Cost-free check of the key in effect after saving.</summary>
    private async Task CheckConnectionAsync(SupertextSettings settings)
    {
        var apiKey = configuration.ApiKeyFromEnvironment != string.Empty ? configuration.ApiKeyFromEnvironment : configuration.Unprotect(settings.ProtectedApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await notifier.WarningAsync(H["No Supertext API key is set yet, so content can't be translated. Generate one at <a href=\"{0}\" target=\"_blank\" rel=\"noopener\">supertext.com → Integrations → API</a> (requires the Admin role; no account yet? <a href=\"{1}\" target=\"_blank\" rel=\"noopener\">create one</a>).", "https://www.supertext.com/en/integrations/api", "https://www.supertext.com/person/en/account/signin"]);
            return;
        }
        var endpoint = configuration.EndpointFromEnvironment != string.Empty ? configuration.EndpointFromEnvironment
            : string.IsNullOrWhiteSpace(settings.Endpoint) ? SupertextClient.DefaultEndpoint : settings.Endpoint;
        try
        {
            await client.ValidateApiKeyAsync(new SupertextConnection(endpoint, apiKey));
            await notifier.SuccessAsync(H["Connected to Supertext. The API key works."]);
        }
        catch (SupertextException e)
        {
            await notifier.ErrorAsync(H["The settings were saved, but Supertext could not be reached with them: {0}", e.Message]);
        }
    }
}
