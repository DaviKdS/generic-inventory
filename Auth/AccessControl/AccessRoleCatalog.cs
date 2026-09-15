namespace GenericInventory.Auth.AccessControl;

/// <summary>
/// Fonte unica da hierarquia de acesso. Backend, policies e painel de acessos leem daqui,
/// de modo que a matriz de permissoes exista em um lugar so.
/// </summary>
public static class AccessRoleCatalog
{
    public const string Developer = "developer";
    public const string Admin = "admin";
    public const string Standard = "standard";

    /// <summary>Papel atribuido a quem apenas solicitou acesso; nao concede nada ate a aprovacao.</summary>
    public const string DefaultRole = Standard;

    private static readonly IReadOnlyList<AccessRole> Catalog = new List<AccessRole>
    {
        new()
        {
            Name = Developer,
            Level = 1000,
            Label = "Developer",
            Description = "Acesso tecnico total: define perfis, telas, catalogo, importacoes e administracao do sistema.",
            Permissions = Set(AccessPermissions.All.ToArray())
        },
        new()
        {
            Name = Admin,
            Level = 100,
            Label = "Administrador",
            Description = "Gerencia acessos, produtos, funcionarios, movimentacoes e lembretes de estoque.",
            Permissions = Set(
                AccessPermissions.AccessManage,
                AccessPermissions.StockRead,
                AccessPermissions.StockMove,
                AccessPermissions.ProductsManage,
                AccessPermissions.EmployeesManage,
                AccessPermissions.RemindersManage)
        },
        new()
        {
            Name = Standard,
            Level = 10,
            Label = "Padrao",
            Description = "Consulta estoque e registra entradas e saidas.",
            Permissions = Set(
                AccessPermissions.StockRead,
                AccessPermissions.StockMove)
        }
    };

    private static readonly IReadOnlyDictionary<string, AccessRole> ByName =
        Catalog.ToDictionary(role => role.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Papeis do mais alto para o mais baixo.</summary>
    public static IReadOnlyList<AccessRole> All => Catalog;

    public static AccessRole? Find(string? role)
    {
        return role != null && ByName.TryGetValue(role.Trim(), out var found) ? found : null;
    }

    public static bool Exists(string? role)
    {
        return Find(role) != null;
    }

    /// <summary>Devolve o papel em forma canonica, caindo no papel padrao quando o valor e desconhecido.</summary>
    public static string Normalize(string? role)
    {
        return Find(role)?.Name ?? DefaultRole;
    }

    public static int LevelOf(string? role)
    {
        return Find(role)?.Level ?? 0;
    }

    public static IReadOnlySet<string> PermissionsOf(string? role)
    {
        return Find(role)?.Permissions ?? Set();
    }

    public static bool Grants(string? role, string permission)
    {
        return PermissionsOf(role).Contains(permission);
    }

    public static bool IsAdmin(string? role)
    {
        var name = Find(role)?.Name;
        return string.Equals(name, Admin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, Developer, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDeveloper(string? role)
    {
        return string.Equals(Find(role)?.Name, Developer, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Papeis que uma decisao vinda do fluxo de e-mail pode conceder.
    /// Elevacao a admin exige sessao autenticada no painel, nunca um token de callback.
    /// </summary>
    public static bool IsDelegableByCallback(string? role)
    {
        var found = Find(role);
        return found != null && found.Level < LevelOf(Admin);
    }

    private static IReadOnlySet<string> Set(params string[] permissions)
    {
        return new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
    }
}

