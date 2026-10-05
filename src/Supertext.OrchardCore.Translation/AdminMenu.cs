using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;
using Supertext.OrchardCore.Translation.Drivers;

namespace Supertext.OrchardCore.Translation;

public sealed class AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer) : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", SupertextSettingsDisplayDriver.GroupId },
    };

    private readonly IStringLocalizer S = stringLocalizer;

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (NavigationHelper.UseLegacyFormat())
        {
            builder.Add(S["Configuration"], configuration => configuration
                .Add(S["Settings"], settings => settings
                    .Add(S["Supertext"], S["Supertext"].PrefixPosition(), supertext => supertext
                        .AddClass("supertext").Id("supertext")
                        .Action("Index", "Admin", _routeValues)
                        .Permission(SupertextPermissions.ManageSupertextSettings)
                        .LocalNav())));
            return ValueTask.CompletedTask;
        }

        builder.Add(S["Settings"], settings => settings
            .Add(S["Supertext"], S["Supertext"].PrefixPosition(), supertext => supertext
                .AddClass("supertext").Id("supertext")
                .Action("Index", "Admin", _routeValues)
                .Permission(SupertextPermissions.ManageSupertextSettings)
                .LocalNav()));
        return ValueTask.CompletedTask;
    }
}
