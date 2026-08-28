using GenericInventory.Auth.AccessControl;

namespace GenericInventory.Auth.Dtos;

public class UserAccessDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = AccessRoleCatalog.DefaultRole;
    public string RoleLabel { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Status { get; set; } = AccessStatus.Pending;
    public string Origin { get; set; } = AccessOrigin.SelfService;

    /// <summary>Verdadeiro enquanto o convite de senha nao foi concluido.</summary>
    public bool MustDefinePassword { get; set; }

    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
    public string InvitedBy { get; set; } = string.Empty;
    public DateTimeOffset? SuspendedAt { get; set; }
    public string SuspensionReason { get; set; } = string.Empty;
    public string RejectionReason { get; set; } = string.Empty;
}

