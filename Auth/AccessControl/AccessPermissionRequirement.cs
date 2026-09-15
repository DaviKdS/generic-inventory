using Microsoft.AspNetCore.Authorization;

namespace GenericInventory.Auth.AccessControl;

/// <summary>
/// Exige uma permissão nomeada do catálogo de papéis.
/// </summary>
public sealed class AccessPermissionRequirement : IAuthorizationRequirement
{
    public AccessPermissionRequirement(string permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}

