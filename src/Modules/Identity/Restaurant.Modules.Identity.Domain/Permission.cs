namespace Restaurant.Modules.Identity.Domain;

public sealed class Permission
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class RolePermission
{
    public Guid RoleId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
}
