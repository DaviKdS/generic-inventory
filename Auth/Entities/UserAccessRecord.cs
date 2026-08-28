using GenericInventory.Auth.AccessControl;

namespace GenericInventory.Auth.Entities;

public class UserAccessRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Vazio enquanto o usuario ainda nao definiu a senha pelo link enviado por e-mail.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Token de decisao usado pelo fluxo de aprovacao por e-mail/Teams.</summary>
    public string ApprovalTokenHash { get; set; } = string.Empty;
    public DateTimeOffset? ApprovalTokenCreatedAt { get; set; }
    public DateTimeOffset? ApprovalTokenUsedAt { get; set; }

    /// <summary>Token de uso unico para definir ou redefinir a senha.</summary>
    public string PasswordTokenHash { get; set; } = string.Empty;
    public DateTimeOffset? PasswordTokenCreatedAt { get; set; }
    public DateTimeOffset? PasswordTokenUsedAt { get; set; }

    public string Role { get; set; } = AccessRoleCatalog.DefaultRole;
    public string Status { get; set; } = AccessStatus.Pending;

    /// <summary>Como o acesso entrou no sistema: bootstrap, invite ou self-service.</summary>
    public string Origin { get; set; } = AccessOrigin.SelfService;

    /// <summary>Muda a cada alteracao sensivel para invalidar sessoes abertas.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;

    public string InvitedBy { get; set; } = string.Empty;

    public DateTimeOffset? ApprovedAt { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;

    public DateTimeOffset? RejectedAt { get; set; }
    public string RejectedBy { get; set; } = string.Empty;
    public string RejectionReason { get; set; } = string.Empty;

    public DateTimeOffset? SuspendedAt { get; set; }
    public string SuspendedBy { get; set; } = string.Empty;
    public string SuspensionReason { get; set; } = string.Empty;

    public bool MustDefinePassword => string.IsNullOrWhiteSpace(PasswordHash);

    public void Touch(string changedBy)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = changedBy;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }
}

