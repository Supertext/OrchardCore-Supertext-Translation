using OrchardCore;
using OrchardCore.Security.Permissions;

namespace Supertext.OrchardCore.Translation;

public static class SupertextPermissions
{
    public static readonly Permission TranslateWithSupertext = new("TranslateWithSupertext", "Translate content with Supertext");

    public static readonly Permission ManageSupertextSettings = new("ManageSupertextSettings", "Manage Supertext settings");
}

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        SupertextPermissions.TranslateWithSupertext,
        SupertextPermissions.ManageSupertextSettings,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync() => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype { Name = OrchardCoreConstants.Roles.Administrator, Permissions = _allPermissions },
        new PermissionStereotype { Name = OrchardCoreConstants.Roles.Editor, Permissions = [SupertextPermissions.TranslateWithSupertext] },
    ];
}
