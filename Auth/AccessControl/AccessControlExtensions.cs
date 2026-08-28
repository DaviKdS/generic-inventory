using Microsoft.AspNetCore.Authorization;

namespace GenericInventory.Auth.AccessControl;

public static class AccessControlExtensions
{
    /// <summary>
    /// Registra uma policy por permissao do catalogo. Os controllers passam a declarar a permissao
    /// exigida em vez de um papel fixo, entao mudar a matriz nao exige mexer em cada endpoint.
    /// </summary>
    public static IServiceCollection AddAccessControl(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, AccessPermissionHandler>();

        services.AddAuthorization(options =>
        {
            foreach (var permission in AccessPermissions.All)
            {
                options.AddPolicy(permission, policy => policy.AddRequirements(new AccessPermissionRequirement(permission)));
            }

            // Nome historico da policy, mantido para nao quebrar endpoints e testes existentes.
            options.AddPolicy(AccessRoleCatalog.Admin, policy =>
                policy.AddRequirements(new AccessPermissionRequirement(AccessPermissions.AccessManage)));
        });

        return services;
    }
}

