namespace GenericInventory.Auth.Dtos;

/// <summary>
/// Hierarquia publicada para o painel de acessos: papeis, permissoes e a matriz entre eles.
/// </summary>
public class AccessCatalogDto
{
    public IReadOnlyList<AccessRoleDto> Roles { get; set; } = Array.Empty<AccessRoleDto>();
    public IReadOnlyList<AccessPermissionDto> Permissions { get; set; } = Array.Empty<AccessPermissionDto>();
    public IReadOnlyList<string> Statuses { get; set; } = Array.Empty<string>();
    public string DefaultRole { get; set; } = string.Empty;
}

