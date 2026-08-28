using Microsoft.AspNetCore.Authorization;

namespace GenericInventory.Auth.AccessControl;

/// <summary>
/// Exige uma permissao nomeada do catalogo de papeis.
/// </summary>
public sealed class AccessPermissionRequirement : IAuthorizationRequirement
{
    public AccessPermissionRequirement(string permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}

