using GenericInventory.Auth.Dtos;
using GenericInventory.Auth.Entities;

namespace GenericInventory.Auth.Interfaces;

public interface IUserAccessService
{
    string ApproverEmail { get; }

    bool SelfRegistrationEnabled { get; }

    /// <summary>Garante que a conta administradora configurada exista e esteja ativa.</summary>
    Task EnsureBootstrapAdminAsync(CancellationToken cancellationToken = default);

    Task<UserAccessDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<UserAccessRecord?> ValidateLoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<UserAccessDto?> GetUserByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Confere se a sessao ainda corresponde ao acesso atual (papel, status e senha).</summary>
    Task<bool> IsSessionValidAsync(string id, string securityStamp, CancellationToken cancellationToken = default);

    Task<IEnumerable<UserAccessDto>> GetUsersAsync(UserQueryDto query, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserAccessDto>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default);
    Task<int> CountPendingApprovalsAsync(CancellationToken cancellationToken = default);

    Task<UserAccessDto> ApproveAsync(string id, string role, string approvedBy, CancellationToken cancellationToken = default);
    Task<UserAccessDto> RejectAsync(string id, string rejectedBy, string reason, CancellationToken cancellationToken = default);
    Task<UserAccessDto> DecideWithTokenAsync(string id, ApprovalCallbackDto request, CancellationToken cancellationToken = default);

    /// <summary>Cria um acesso ja liberado e envia o convite de senha.</summary>
    Task<UserAccessDto> InviteAsync(InviteUserRequestDto request, string invitedBy, CancellationToken cancellationToken = default);

    Task<UserAccessDto> ChangeRoleAsync(string id, string role, string changedBy, CancellationToken cancellationToken = default);
    Task<UserAccessDto> SuspendAsync(string id, string suspendedBy, string reason, CancellationToken cancellationToken = default);
    Task<UserAccessDto> ReactivateAsync(string id, string reactivatedBy, CancellationToken cancellationToken = default);
    Task RemoveAsync(string id, string removedBy, CancellationToken cancellationToken = default);

    /// <summary>Dispara o link de senha a pedido do administrador.</summary>
    Task<UserAccessDto> SendPasswordLinkAsync(string id, string requestedBy, CancellationToken cancellationToken = default);

    /// <summary>Dispara o link de senha a pedido do proprio usuario. Nunca revela se o e-mail existe.</summary>
    Task RequestPasswordLinkAsync(string email, CancellationToken cancellationToken = default);

    Task<UserAccessDto> ResetPasswordAsync(string id, PasswordResetRequestDto request, CancellationToken cancellationToken = default);
}

