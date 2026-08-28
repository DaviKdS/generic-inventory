using GenericInventory.Auth.Entities;

namespace GenericInventory.Auth.Interfaces;

public interface IApprovalNotifier
{
    Task SendApprovalRequestAsync(UserAccessRecord user, string approvalToken, CancellationToken cancellationToken = default);
    Task SendAccessApprovedAsync(UserAccessRecord user, CancellationToken cancellationToken = default);
    Task SendAccessRejectedAsync(UserAccessRecord user, CancellationToken cancellationToken = default);

    /// <summary>Envia o link de uso unico para o usuario definir ou redefinir a propria senha.</summary>
    Task SendPasswordSetupAsync(UserAccessRecord user, string passwordToken, string reason, CancellationToken cancellationToken = default);

    /// <summary>Avisa o usuario que o papel dele mudou.</summary>
    Task SendRoleChangedAsync(UserAccessRecord user, string previousRole, CancellationToken cancellationToken = default);

    /// <summary>Avisa o usuario sobre suspensao, reativacao ou remocao do acesso.</summary>
    Task SendAccessChangedAsync(UserAccessRecord user, string action, CancellationToken cancellationToken = default);
}

