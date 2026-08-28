namespace GenericInventory.Auth.Dtos;

public class AuthSessionDto
{
    public bool IsAuthenticated { get; set; }
    public UserAccessDto? User { get; set; }

    /// <summary>Permissoes efetivas da sessao. O front usa para habilitar acoes.</summary>
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();

    public bool CanManageAccess { get; set; }
    public int PendingApprovals { get; set; }
    public string ApproverEmail { get; set; } = string.Empty;
    public bool SelfRegistrationEnabled { get; set; }
}

