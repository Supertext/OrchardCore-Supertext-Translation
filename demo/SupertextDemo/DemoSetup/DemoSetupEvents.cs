using Microsoft.AspNetCore.Identity;
using OrchardCore.ContentLocalization.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Localization.Models;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Title.Models;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using YesSql;

namespace SupertextDemo.DemoSetup;

public static class DemoSetupServiceCollectionExtensions
{
    public static IServiceCollection AddDemoSetup(this IServiceCollection services)
        => services.AddScoped<IModularTenantEvents, DemoSetupEvents>();
}

/// <summary>
/// Runs on every start of the (set up) tenant: makes sure the demo languages, the demo
/// accounts from DEMO_* variables, the editor's permissions and an English sample article
/// exist. Never changes existing accounts.
/// </summary>
public sealed class DemoSetupEvents(ShellSettings shellSettings, ILogger<DemoSetupEvents> logger) : ModularTenantEvents
{
    public static readonly string[] Cultures = ["en", "de-CH", "fr-CH", "it-CH"];

    /// <summary>Permissions the editor account needs to translate in every language.</summary>
    private static readonly string[] EditorPermissions = ["LocalizeContent", "TranslateWithSupertext", "EditContent", "PublishContent", "PreviewContent", "ViewContent", "AccessAdminPanel"];

    public override Task ActivatedAsync()
    {
        if (!shellSettings.IsRunning())
        {
            return Task.CompletedTask;
        }

        // After the activation scope, in a scope of its own (all tenant services are ready).
        ShellScope.AddDeferredTask(async scope =>
        {
            try
            {
                var services = scope.ServiceProvider;
                await EnsureCulturesAsync(services);
                await EnsureEditorRoleAsync(services);
                await EnsureAccountAsync(services, "DEMO_ADMIN", "Administrator", fallbackPrefix: "ORCHARD_ADMIN");
                await EnsureAccountAsync(services, "DEMO_EDITOR", "Editor");
                await EnsureSampleContentAsync(services);
                logger.LogInformation("[demo] setup complete");
            }
            catch (Exception e)
            {
                logger.LogError(e, "[demo] setup failed");
            }
        });
        return Task.CompletedTask;
    }

    private async Task EnsureCulturesAsync(IServiceProvider services)
    {
        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.LoadSiteSettingsAsync();
        var settings = site.TryGet<LocalizationSettings>(out var existing) ? existing : new LocalizationSettings();
        var missing = Cultures.Where(c => !(settings.SupportedCultures ?? []).Contains(c, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (missing.Length == 0)
        {
            return;
        }
        settings.SupportedCultures = [.. settings.SupportedCultures ?? [], .. missing];
        if (string.IsNullOrEmpty(settings.DefaultCulture))
        {
            settings.DefaultCulture = "en";
        }
        site.Put(settings);
        await siteService.UpdateSiteSettingsAsync(site);
        services.GetRequiredService<IShellReleaseManager>().RequestRelease();
        logger.LogInformation("[demo] added cultures {Cultures}", string.Join(", ", missing));
    }

    private async Task EnsureEditorRoleAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IRole>>();
        if (await roleManager.FindByNameAsync("Editor") is not Role role)
        {
            role = new Role { RoleName = "Editor", RoleDescription = "Grants users the ability to edit, translate and publish content." };
            await roleManager.CreateAsync(role);
            role = (Role)await roleManager.FindByNameAsync("Editor");
        }
        var missing = EditorPermissions.Where(p => !role.RoleClaims.Any(c => c.ClaimType == Permission.ClaimType && c.ClaimValue == p)).ToList();
        if (missing.Count == 0)
        {
            return;
        }
        foreach (var permission in missing)
        {
            role.RoleClaims.Add(new RoleClaim { ClaimType = Permission.ClaimType, ClaimValue = permission });
        }
        await roleManager.UpdateAsync(role);
        logger.LogInformation("[demo] granted the Editor role: {Permissions}", string.Join(", ", missing));
    }

    private async Task EnsureAccountAsync(IServiceProvider services, string prefix, string roleName, string fallbackPrefix = null)
    {
        var emailVariable = $"{prefix}_EMAIL";
        var passwordVariable = $"{prefix}_PASSWORD";
        var email = Environment.GetEnvironmentVariable(emailVariable);
        var password = Environment.GetEnvironmentVariable(passwordVariable);
        if (string.IsNullOrWhiteSpace(email) && fallbackPrefix is not null)
        {
            emailVariable = $"{fallbackPrefix}_EMAIL";
            passwordVariable = $"{fallbackPrefix}_PASSWORD";
            email = Environment.GetEnvironmentVariable(emailVariable);
            password = Environment.GetEnvironmentVariable(passwordVariable);
        }
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            logger.LogInformation("[demo] {Variable} not set, no {Role} account created", $"{prefix}_EMAIL/{prefix}_PASSWORD", roleName);
            return;
        }
        email = email.Trim();

        var userManager = services.GetRequiredService<UserManager<IUser>>();
        if (await userManager.FindByEmailAsync(email) is not null || await userManager.FindByNameAsync(UserNameFor(email)) is not null)
        {
            return; // Existing accounts are never modified.
        }

        var user = new User
        {
            UserName = UserNameFor(email),
            Email = email,
            EmailConfirmed = true,
            IsEnabled = true,
            RoleNames = [roleName],
        };
        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            logger.LogInformation("[demo] created the {Role} account from {Variable}", roleName, emailVariable);
        }
        else
        {
            // Never log the password, only which variable to fix.
            logger.LogWarning("[demo] skipped the {Role} account: {Variable} does not meet the password rules ({Errors})",
                roleName, passwordVariable, string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }

    /// <summary>
    /// Orchard Core user names can't contain "@": the user name is the e-mail's local part
    /// (same rule as demo/entrypoint.sh). Users sign in with the e-mail address.
    /// </summary>
    public static string UserNameFor(string email)
    {
        var local = email.Split('@')[0];
        var name = new string(local.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '+').ToArray());
        return name == string.Empty ? "demo" : name;
    }

    private async Task EnsureSampleContentAsync(IServiceProvider services)
    {
        var session = services.GetRequiredService<YesSql.ISession>();
        var count = await session.QueryIndex<ContentItemIndex>(i => i.ContentType == "Article" && i.Latest).CountAsync();
        if (count > 0)
        {
            return;
        }
        var contentManager = services.GetRequiredService<IContentManager>();
        var article = await contentManager.NewAsync("Article");
        article.Alter<TitlePart>(p => p.Title = "Translating with Supertext");
        article.Alter<LocalizationPart>(p => p.Culture = "en");
        article.DisplayText = "Translating with Supertext";
        var data = (System.Text.Json.Nodes.JsonObject)(System.Text.Json.Dynamic.JsonDynamicObject)article.Content;
        data["HtmlBodyPart"] = new System.Text.Json.Nodes.JsonObject { ["Html"] = "<p>Supertext translates your content with AI that was trained on the work of <strong>professional translators</strong>.</p>" };
        data["Article"] = new System.Text.Json.Nodes.JsonObject { ["Subtitle"] = new System.Text.Json.Nodes.JsonObject { ["Text"] = "Your content, in every language your customers speak" } };
        await contentManager.CreateAsync(article, VersionOptions.Published);
        logger.LogInformation("[demo] created the sample article");
    }
}
