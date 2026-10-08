using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentLocalization.Handlers;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using Supertext.OrchardCore.Translation.Api;
using Supertext.OrchardCore.Translation.Drivers;
using Supertext.OrchardCore.Translation.Handlers;
using Supertext.OrchardCore.Translation.Services;

namespace Supertext.OrchardCore.Translation;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient<SupertextClient>(http => http.Timeout = TimeSpan.FromSeconds(100));
        services.AddScoped<SupertextConfiguration>();
        services.AddScoped<SupertextScope>();
        services.AddScoped<ContentTranslator>();
        services.AddScoped<SupertextMessages>();
        services.AddScoped<IContentLocalizationHandler, SupertextLocalizationHandler>();

        services.AddSiteDisplayDriver<SupertextSettingsDisplayDriver>();
        services.AddScoped<IContentDisplayDriver, SupertextContentDisplayDriver>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddPermissionProvider<Permissions>();
    }
}
