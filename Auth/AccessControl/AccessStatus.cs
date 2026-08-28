namespace GenericInventory.Auth.AccessControl;

/// <summary>
/// Estados do ciclo de vida de um acesso.
/// </summary>
public static class AccessStatus
{
    /// <summary>Solicitou acesso e aguarda decisao do administrador.</summary>
    public const string Pending = "pending";

    /// <summary>Acesso liberado pelo administrador.</summary>
    public const string Approved = "approved";

    /// <summary>Solicitacao recusada.</summary>
    public const string Rejected = "rejected";

    /// <summary>Acesso bloqueado temporariamente sem perder o historico.</summary>
    public const string Suspended = "suspended";

    public static IReadOnlyList<string> All { get; } = new[] { Pending, Approved, Rejected, Suspended };

    public static bool IsActive(string? status)
    {
        return string.Equals(status, Approved, StringComparison.OrdinalIgnoreCase);
    }

    public static bool Exists(string? status)
    {
        return status != null && All.Contains(status, StringComparer.OrdinalIgnoreCase);
    }
}

