using OrchardCore.Security.Permissions;

namespace OrchardFramework.Modules.Template.Permissions;

public sealed class TemplatePermissions : IPermissionProvider
{
    public static readonly Permission ManageModuleTemplate = new("ManageModuleTemplate", "Manage module template");

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageModuleTemplate,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = "Administrator",
            Permissions = _allPermissions,
        },
    ];
}
