using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace GenericInventory.Auth.AccessControl;

/// <summary>
/// Resolve uma permissao a partir do papel gravado na sessao local.
/// </summary>
public sealed class AccessPermissionHandler : AuthorizationHandler<AccessPermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AccessPermissionRequirement requirement)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // Um acesso suspenso ou recusado mantem o cookie ate a proxima validacao; nao concede nada.
        var status = user.FindFirstValue(AccessClaims.Status);
        if (status != null && !AccessStatus.IsActive(status))
        {
            return Task.CompletedTask;
        }

        if (AccessRoleCatalog.Grants(user.FindFirstValue(ClaimTypes.Role), requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

