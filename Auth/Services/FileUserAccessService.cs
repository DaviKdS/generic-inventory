using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using GenericInventory.Auth.AccessControl;
using GenericInventory.Auth.Dtos;
using GenericInventory.Auth.Entities;
using GenericInventory.Auth.Interfaces;
using GenericInventory.Auth.Options;

namespace GenericInventory.Auth.Services;

/// <summary>
/// Guarda os acessos em arquivo JSON e concentra as regras da hierarquia.
/// Toda concessao de papel passa por aqui, entao as invariantes ficam em um lugar so.
/// </summary>
public class FileUserAccessService : IUserAccessService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _storePath;
    private readonly AuthOptions _options;
    private readonly IApprovalNotifier _approvalNotifier;
    private readonly ILogger<FileUserAccessService> _logger;

    public FileUserAccessService(
        IOptions<AuthOptions> options,
        IWebHostEnvironment environment,
        IApprovalNotifier approvalNotifier,
        ILogger<FileUserAccessService> logger)
    {
        _options = options.Value;
        _approvalNotifier = approvalNotifier;
        _logger = logger;
        _storePath = Path.IsPathRooted(_options.StorePath)
            ? _options.StorePath
            : Path.Combine(environment.ContentRootPath, _options.StorePath);
    }

    public string ApproverEmail => NormalizeEmail(_options.ApproverEmail);

    public bool SelfRegistrationEnabled => _options.AllowSelfRegistration;

    public async Task EnsureBootstrapAdminAsync(CancellationToken cancellationToken = default)
    {
        var bootstrap = _options.Bootstrap;
        if (!bootstrap.Enabled)
        {
            return;
        }

        var email = NormalizeEmail(string.IsNullOrWhiteSpace(bootstrap.Email) ? _options.ApproverEmail : bootstrap.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Bootstrap de acesso ignorado: nenhum e-mail de administrador configurado.");
            return;
        }

        UserAccessRecord? notifyUser = null;
        var passwordToken = string.Empty;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadUsersAsync(cancellationToken);
            var admin = FindByEmail(users, email);

            if (admin == null)
            {
                admin = new UserAccessRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = string.IsNullOrWhiteSpace(bootstrap.Name) ? "Administrador" : bootstrap.Name.Trim(),
                    Email = email,
                    PasswordHash = string.Empty,
                    Role = AccessRoleCatalog.Admin,
                    Status = AccessStatus.Approved,
                    Origin = AccessOrigin.Bootstrap,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ApprovedAt = DateTimeOffset.UtcNow,
                    ApprovedBy = AccessOrigin.Bootstrap
                };

                users.Add(admin);
                _logger.LogInformation("Conta administradora criada pelo bootstrap para {Email}.", email);
            }

            // Reparo defensivo: a conta configurada nunca pode ficar sem admin nem suspensa.
            var repaired = false;
            if (!AccessRoleCatalog.IsAdmin(admin.Role))
            {
                admin.Role = AccessRoleCatalog.Admin;
                repaired = true;
            }

            if (!AccessStatus.IsActive(admin.Status))
            {
                admin.Status = AccessStatus.Approved;
                admin.ApprovedAt ??= DateTimeOffset.UtcNow;
                admin.SuspendedAt = null;
                admin.SuspendedBy = string.Empty;
                admin.SuspensionReason = string.Empty;
                repaired = true;
            }

            if (repaired)
            {
                admin.Touch(AccessOrigin.Bootstrap);
                _logger.LogInformation("Conta administradora de bootstrap restaurada para {Email}.", email);
            }

            if (admin.MustDefinePassword && TryReadSeededPasswordHash(bootstrap, out var seededHash))
            {
                // Senha inicial vinda do cofre de segredos do ambiente. Dispensa o link por e-mail.
                admin.PasswordHash = seededHash;
                admin.PasswordTokenHash = string.Empty;
                admin.PasswordTokenCreatedAt = null;
                admin.PasswordTokenUsedAt = null;
                admin.Touch(AccessOrigin.Bootstrap);
                _logger.LogInformation("Senha inicial da conta administradora aplicada a partir da configuracao.");
            }
            else if (admin.MustDefinePassword && !HasPendingPasswordToken(admin))
            {
                // Envia o link apenas quando nao ha senha e nao existe convite valido em aberto.
                passwordToken = IssuePasswordToken(admin);
                notifyUser = admin;
            }

            await SaveUsersAsync(users, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        if (notifyUser is { } bootstrapAdmin)
        {
            await NotifyAsync(() => _approvalNotifier.SendPasswordSetupAsync(
                bootstrapAdmin, passwordToken, PasswordSetupReason.Bootstrap, cancellationToken));
        }
    }

    public async Task<UserAccessDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!_options.AllowSelfRegistration)
        {
            throw new InvalidOperationException("As solicitacoes de acesso estao fechadas. Peca um convite ao administrador.");
        }

        var name = request.Name.Trim();
        var email = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Informe o nome.");
        }

        var approvalToken = GenerateToken();
        UserAccessRecord createdUser;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadUsersAsync(cancellationToken);
            if (FindByEmail(users, email) != null)
            {
                throw new InvalidOperationException("Este e-mail ja possui um cadastro.");
            }

            // A solicitacao nasce sem poder algum: papel e status so mudam por decisao do administrador.
            createdUser = new UserAccessRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Email = email,
                PasswordHash = PasswordHashingService.Hash(request.Password),
                ApprovalTokenHash = PasswordHashingService.Hash(approvalToken),
                ApprovalTokenCreatedAt = DateTimeOffset.UtcNow,
                Role = AccessRoleCatalog.DefaultRole,
                Status = AccessStatus.Pending,
                Origin = AccessOrigin.SelfService,
                CreatedAt = DateTimeOffset.UtcNow
            };

            users.Add(createdUser);
            await SaveUsersAsync(users, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        await NotifyAsync(() => _approvalNotifier.SendApprovalRequestAsync(createdUser, approvalToken, cancellationToken));
        return ToDto(createdUser);
    }

    public async Task<UserAccessRecord?> ValidateLoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var user = FindByEmail(await LoadUsersAsync(cancellationToken), email);
            if (user == null)
            {
                return null;
            }

            if (user.MustDefinePassword)
            {
                throw new UnauthorizedAccessException("Defina sua senha pelo link enviado por e-mail.");
            }

            if (!PasswordHashingService.Verify(request.Password, user.PasswordHash))
            {
                return null;
            }

            EnsureCanSignIn(user);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccessDto?> GetUserByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var user = (await LoadUsersAsync(cancellationToken)).FirstOrDefault(item => item.Id == id);
            return user == null ? null : ToDto(user);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> IsSessionValidAsync(string id, string securityStamp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(securityStamp))
        {
            return false;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var user = (await LoadUsersAsync(cancellationToken)).FirstOrDefault(item => item.Id == id);
            return user != null
                && AccessStatus.IsActive(user.Status)
                && string.Equals(user.SecurityStamp, securityStamp, StringComparison.Ordinal);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IEnumerable<UserAccessDto>> GetUsersAsync(UserQueryDto query, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = (await LoadUsersAsync(cancellationToken)).AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                users = users.Where(user => string.Equals(user.Status, query.Status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.Role))
            {
                users = users.Where(user => string.Equals(user.Role, query.Role, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                users = users.Where(user =>
                    user.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    user.Email.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            return users
                .OrderByDescending(user => AccessRoleCatalog.LevelOf(user.Role))
                .ThenBy(user => user.Name, StringComparer.OrdinalIgnoreCase)
                .Select(ToDto)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IEnumerable<UserAccessDto>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadUsersAsync(cancellationToken))
                .Where(user => string.Equals(user.Status, AccessStatus.Pending, StringComparison.OrdinalIgnoreCase))
                .OrderBy(user => user.CreatedAt)
                .Select(ToDto)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> CountPendingApprovalsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadUsersAsync(cancellationToken))
                .Count(user => string.Equals(user.Status, AccessStatus.Pending, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccessDto> ApproveAsync(string id, string role, string approvedBy, CancellationToken cancellationToken = default)
    {
        EnsureKnownRole(role);
        var updatedUser = await MutateAsync(id, (users, user) => ApproveUser(user, role, approvedBy), cancellationToken);

        await NotifyAsync(() => _approvalNotifier.SendAccessApprovedAsync(updatedUser, cancellationToken));
        return ToDto(updatedUser);
    }

    public async Task<UserAccessDto> RejectAsync(string id, string rejectedBy, string reason, CancellationToken cancellationToken = default)
    {
        var updatedUser = await MutateAsync(id, (users, user) =>
        {
            EnsureNotSelf(user, rejectedBy);
            EnsureNotLastActiveAdmin(users, user);
            RejectUser(user, rejectedBy, reason);
        }, cancellationToken);

        await NotifyAsync(() => _approvalNotifier.SendAccessRejectedAsync(updatedUser, cancellationToken));
        return ToDto(updatedUser);
    }

    public async Task<UserAccessDto> DecideWithTokenAsync(string id, ApprovalCallbackDto request, CancellationToken cancellationToken = default)
    {
        var decision = request.Decision.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new UnauthorizedAccessException("Token de aprovacao ausente.");
        }

        var updatedUser = await MutateAsync(id, (users, user) =>
        {
            ValidateApprovalToken(user, request.Token);
            var decidedBy = string.IsNullOrWhiteSpace(request.DecidedBy) ? "power-automate" : request.DecidedBy;

            if (decision is "approve" or "approved")
            {
                // Um token que trafega por e-mail nunca promove a admin.
                if (!AccessRoleCatalog.IsDelegableByCallback(request.Role))
                {
                    throw new InvalidOperationException("Este papel so pode ser concedido no painel de acessos.");
                }

                ApproveUser(user, request.Role, decidedBy);
            }
            else if (decision is "reject" or "rejected")
            {
                RejectUser(user, decidedBy, request.Reason);
            }
            else
            {
                throw new InvalidOperationException("Decisao invalida.");
            }

            user.ApprovalTokenUsedAt = DateTimeOffset.UtcNow;
        }, cancellationToken);

        if (AccessStatus.IsActive(updatedUser.Status))
        {
            await NotifyAsync(() => _approvalNotifier.SendAccessApprovedAsync(updatedUser, cancellationToken));
        }
        else
        {
            await NotifyAsync(() => _approvalNotifier.SendAccessRejectedAsync(updatedUser, cancellationToken));
        }

        return ToDto(updatedUser);
    }

    public async Task<UserAccessDto> InviteAsync(InviteUserRequestDto request, string invitedBy, CancellationToken cancellationToken = default)
    {
        EnsureKnownRole(request.Role);

        var name = request.Name.Trim();
        var email = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Informe o nome.");
        }

        UserAccessRecord createdUser;
        string passwordToken;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadUsersAsync(cancellationToken);
            if (FindByEmail(users, email) != null)
            {
                throw new InvalidOperationException("Este e-mail ja possui um acesso.");
            }

            createdUser = new UserAccessRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Email = email,
                PasswordHash = string.Empty,
                Role = AccessRoleCatalog.Normalize(request.Role),
                Status = AccessStatus.Approved,
                Origin = AccessOrigin.Invite,
                InvitedBy = invitedBy,
                CreatedAt = DateTimeOffset.UtcNow,
                ApprovedAt = DateTimeOffset.UtcNow,
                ApprovedBy = invitedBy
            };

            passwordToken = IssuePasswordToken(createdUser);
            users.Add(createdUser);
            await SaveUsersAsync(users, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        await NotifyAsync(() => _approvalNotifier.SendPasswordSetupAsync(
            createdUser, passwordToken, PasswordSetupReason.Invite, cancellationToken));

        return ToDto(createdUser);
    }

    public async Task<UserAccessDto> ChangeRoleAsync(string id, string role, string changedBy, CancellationToken cancellationToken = default)
    {
        EnsureKnownRole(role);
        var normalizedRole = AccessRoleCatalog.Normalize(role);
        var previousRole = string.Empty;

        var updatedUser = await MutateAsync(id, (users, user) =>
        {
            previousRole = user.Role;
            if (string.Equals(previousRole, normalizedRole, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Um admin nao muda o proprio papel: evita se trancar para fora do painel.
            EnsureNotSelf(user, changedBy);

            if (!AccessRoleCatalog.IsAdmin(normalizedRole))
            {
                EnsureNotLastActiveAdmin(users, user);
            }

            user.Role = normalizedRole;
            user.Touch(changedBy);
        }, cancellationToken);

        if (!string.Equals(previousRole, updatedUser.Role, StringComparison.OrdinalIgnoreCase))
        {
            await NotifyAsync(() => _approvalNotifier.SendRoleChangedAsync(updatedUser, previousRole, cancellationToken));
        }

        return ToDto(updatedUser);
    }

    public async Task<UserAccessDto> SuspendAsync(string id, string suspendedBy, string reason, CancellationToken cancellationToken = default)
    {
        var updatedUser = await MutateAsync(id, (users, user) =>
        {
            EnsureNotSelf(user, suspendedBy);
            EnsureNotLastActiveAdmin(users, user);

            user.Status = AccessStatus.Suspended;
            user.SuspendedAt = DateTimeOffset.UtcNow;
            user.SuspendedBy = suspendedBy;
            user.SuspensionReason = reason.Trim();
            user.Touch(suspendedBy);
        }, cancellationToken);

        await NotifyAsync(() => _approvalNotifier.SendAccessChangedAsync(updatedUser, "suspended", cancellationToken));
        return ToDto(updatedUser);
    }

    public async Task<UserAccessDto> ReactivateAsync(string id, string reactivatedBy, CancellationToken cancellationToken = default)
    {
        var updatedUser = await MutateAsync(id, (users, user) =>
        {
            user.Status = AccessStatus.Approved;
            user.ApprovedAt ??= DateTimeOffset.UtcNow;
            user.ApprovedBy = string.IsNullOrWhiteSpace(user.ApprovedBy) ? reactivatedBy : user.ApprovedBy;
            user.SuspendedAt = null;
            user.SuspendedBy = string.Empty;
            user.SuspensionReason = string.Empty;
            user.RejectedAt = null;
            user.RejectedBy = string.Empty;
            user.RejectionReason = string.Empty;
            user.Touch(reactivatedBy);
        }, cancellationToken);

        await NotifyAsync(() => _approvalNotifier.SendAccessChangedAsync(updatedUser, "reactivated", cancellationToken));
        return ToDto(updatedUser);
    }

    public async Task RemoveAsync(string id, string removedBy, CancellationToken cancellationToken = default)
    {
        UserAccessRecord removedUser;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadUsersAsync(cancellationToken);
            var user = users.FirstOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException();

            EnsureNotSelf(user, removedBy);
            EnsureNotLastActiveAdmin(users, user);

            users.Remove(user);
            removedUser = user;
            await SaveUsersAsync(users, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        await NotifyAsync(() => _approvalNotifier.SendAccessChangedAsync(removedUser, "removed", cancellationToken));
    }

    public async Task<UserAccessDto> SendPasswordLinkAsync(string id, string requestedBy, CancellationToken cancellationToken = default)
    {
        var passwordToken = string.Empty;

        var updatedUser = await MutateAsync(id, (users, user) =>
        {
            if (string.Equals(user.Status, AccessStatus.Rejected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cadastro recusado nao recebe link de senha.");
            }

            passwordToken = IssuePasswordToken(user);
            user.UpdatedAt = DateTimeOffset.UtcNow;
            user.UpdatedBy = requestedBy;
        }, cancellationToken);

        await NotifyAsync(() => _approvalNotifier.SendPasswordSetupAsync(
            updatedUser, passwordToken, PasswordSetupReason.Reset, cancellationToken));

        return ToDto(updatedUser);
    }

    public async Task RequestPasswordLinkAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        UserAccessRecord? user;
        var passwordToken = string.Empty;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadUsersAsync(cancellationToken);
            user = FindByEmail(users, normalizedEmail);

            // Silencio proposital: a resposta e a mesma exista ou nao a conta.
            if (user == null || string.Equals(user.Status, AccessStatus.Rejected, StringComparison.OrdinalIgnoreCase))
            {
                user = null;
            }
            else
            {
                passwordToken = IssuePasswordToken(user);
                await SaveUsersAsync(users, cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }

        if (user is { } target)
        {
            await NotifyAsync(() => _approvalNotifier.SendPasswordSetupAsync(
                target, passwordToken, PasswordSetupReason.Reset, cancellationToken));
        }
    }

    public async Task<UserAccessDto> ResetPasswordAsync(string id, PasswordResetRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new UnauthorizedAccessException("Token de senha ausente.");
        }

        var updatedUser = await MutateAsync(id, (users, user) =>
        {
            ValidatePasswordToken(user, request.Token);

            user.PasswordHash = PasswordHashingService.Hash(request.Password);
            user.PasswordTokenHash = string.Empty;
            user.PasswordTokenCreatedAt = null;
            user.PasswordTokenUsedAt = DateTimeOffset.UtcNow;
            user.Touch(user.Email);
        }, cancellationToken);

        return ToDto(updatedUser);
    }

    /// <summary>Carrega, aplica a mutacao sob trava e persiste. Centraliza a leitura/escrita do arquivo.</summary>
    private async Task<UserAccessRecord> MutateAsync(
        string id,
        Action<List<UserAccessRecord>, UserAccessRecord> mutate,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var users = await LoadUsersAsync(cancellationToken);
            var user = users.FirstOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException();

            mutate(users, user);
            await SaveUsersAsync(users, cancellationToken);
            return user;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void EnsureCanSignIn(UserAccessRecord user)
    {
        if (AccessStatus.IsActive(user.Status))
        {
            return;
        }

        throw new UnauthorizedAccessException(user.Status.ToLowerInvariant() switch
        {
            AccessStatus.Rejected => "Cadastro recusado.",
            AccessStatus.Suspended => "Acesso suspenso pelo administrador.",
            _ => "Cadastro aguardando aprovacao."
        });
    }

    private static void EnsureKnownRole(string? role)
    {
        if (!AccessRoleCatalog.Exists(role))
        {
            throw new InvalidOperationException("Papel desconhecido na hierarquia de acesso.");
        }
    }

    private static void EnsureNotSelf(UserAccessRecord user, string actorEmail)
    {
        if (!string.IsNullOrWhiteSpace(actorEmail) &&
            string.Equals(user.Email, NormalizeEmail(actorEmail), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Um administrador nao altera o proprio acesso.");
        }
    }

    private static void EnsureNotLastActiveAdmin(List<UserAccessRecord> users, UserAccessRecord target)
    {
        if (!AccessRoleCatalog.IsAdmin(target.Role) || !AccessStatus.IsActive(target.Status))
        {
            return;
        }

        var otherActiveAdmins = users.Count(user =>
            user.Id != target.Id &&
            AccessRoleCatalog.IsAdmin(user.Role) &&
            AccessStatus.IsActive(user.Status));

        if (otherActiveAdmins == 0)
        {
            throw new InvalidOperationException("Esta e a ultima conta administradora ativa. Promova outro administrador antes.");
        }
    }

    private void ValidateApprovalToken(UserAccessRecord user, string token)
    {
        if (!string.Equals(user.Status, AccessStatus.Pending, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Este cadastro ja foi decidido.");
        }

        if (user.ApprovalTokenUsedAt != null ||
            string.IsNullOrWhiteSpace(user.ApprovalTokenHash) ||
            !PasswordHashingService.Verify(token, user.ApprovalTokenHash))
        {
            throw new UnauthorizedAccessException("Token de aprovacao invalido.");
        }

        var tokenCreatedAt = user.ApprovalTokenCreatedAt ?? user.CreatedAt;
        if (DateTimeOffset.UtcNow - tokenCreatedAt > TimeSpan.FromHours(Math.Max(1, _options.ApprovalTokenHours)))
        {
            throw new UnauthorizedAccessException("Token de aprovacao expirado.");
        }
    }

    private void ValidatePasswordToken(UserAccessRecord user, string token)
    {
        if (string.IsNullOrWhiteSpace(user.PasswordTokenHash) ||
            !PasswordHashingService.Verify(token, user.PasswordTokenHash))
        {
            throw new UnauthorizedAccessException("Link de senha invalido ou ja utilizado.");
        }

        var tokenCreatedAt = user.PasswordTokenCreatedAt ?? user.CreatedAt;
        if (DateTimeOffset.UtcNow - tokenCreatedAt > PasswordTokenLifetime)
        {
            throw new UnauthorizedAccessException("Link de senha expirado. Peca um novo.");
        }
    }

    private TimeSpan PasswordTokenLifetime => TimeSpan.FromHours(Math.Max(1, _options.PasswordTokenHours));

    /// <summary>
    /// Le a senha inicial da configuracao. Exige o formato de hash do PasswordHashingService:
    /// senha em texto puro e recusada, para que o valor no cofre nunca seja a credencial em si.
    /// </summary>
    private bool TryReadSeededPasswordHash(AccessBootstrapOptions bootstrap, out string passwordHash)
    {
        passwordHash = bootstrap.PasswordHash.Trim();
        if (string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split(':');
        if (parts.Length == 4 && parts[0] == "v1" && int.TryParse(parts[1], out var iterations) && iterations > 0)
        {
            return true;
        }

        // Nunca registra o valor recebido em log: ele pode ser uma senha digitada por engano.
        _logger.LogWarning(
            "Auth:Bootstrap:PasswordHash ignorado: formato invalido. Esperado v1:iteracoes:salt:hash.");
        passwordHash = string.Empty;
        return false;
    }

    private bool HasPendingPasswordToken(UserAccessRecord user)
    {
        return !string.IsNullOrWhiteSpace(user.PasswordTokenHash)
            && user.PasswordTokenCreatedAt != null
            && DateTimeOffset.UtcNow - user.PasswordTokenCreatedAt.Value <= PasswordTokenLifetime;
    }

    /// <summary>Gera o token de senha e guarda apenas o hash. O valor em claro so vai para o e-mail.</summary>
    private static string IssuePasswordToken(UserAccessRecord user)
    {
        var token = GenerateToken();
        user.PasswordTokenHash = PasswordHashingService.Hash(token);
        user.PasswordTokenCreatedAt = DateTimeOffset.UtcNow;
        user.PasswordTokenUsedAt = null;
        return token;
    }

    private static void ApproveUser(UserAccessRecord user, string role, string decidedBy)
    {
        user.Role = AccessRoleCatalog.Normalize(role);
        user.Status = AccessStatus.Approved;
        user.ApprovedAt = DateTimeOffset.UtcNow;
        user.ApprovedBy = decidedBy;
        user.RejectedAt = null;
        user.RejectedBy = string.Empty;
        user.RejectionReason = string.Empty;
        user.SuspendedAt = null;
        user.SuspendedBy = string.Empty;
        user.SuspensionReason = string.Empty;
        user.Touch(decidedBy);
    }

    private static void RejectUser(UserAccessRecord user, string decidedBy, string reason)
    {
        user.Status = AccessStatus.Rejected;
        user.RejectedAt = DateTimeOffset.UtcNow;
        user.RejectedBy = decidedBy;
        user.RejectionReason = reason.Trim();
        user.Touch(decidedBy);
    }

    private async Task<List<UserAccessRecord>> LoadUsersAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storePath))
        {
            return new List<UserAccessRecord>();
        }

        await using var stream = File.OpenRead(_storePath);
        return await JsonSerializer.DeserializeAsync<List<UserAccessRecord>>(stream, JsonOptions, cancellationToken)
            ?? new List<UserAccessRecord>();
    }

    private async Task SaveUsersAsync(List<UserAccessRecord> users, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_storePath);
        await JsonSerializer.SerializeAsync(stream, users, JsonOptions, cancellationToken);
    }

    private async Task NotifyAsync(Func<Task> notify)
    {
        try
        {
            await notify();
        }
        catch (Exception ex)
        {
            // A notificacao e um efeito colateral: nunca derruba a operacao de acesso.
            _logger.LogWarning(ex, "Approval notification failed.");
        }
    }

    private static UserAccessRecord? FindByEmail(IEnumerable<UserAccessRecord> users, string email)
    {
        return users.FirstOrDefault(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase));
    }

    private static UserAccessDto ToDto(UserAccessRecord user)
    {
        var role = AccessRoleCatalog.Find(user.Role);

        return new UserAccessDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = role?.Name ?? user.Role,
            RoleLabel = role?.Label ?? user.Role,
            Level = role?.Level ?? 0,
            Status = user.Status,
            Origin = user.Origin,
            MustDefinePassword = user.MustDefinePassword,
            // Acesso pendente, recusado ou suspenso nao carrega permissao alguma.
            Permissions = AccessStatus.IsActive(user.Status)
                ? AccessRoleCatalog.PermissionsOf(user.Role).OrderBy(item => item).ToList()
                : Array.Empty<string>(),
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            ApprovedAt = user.ApprovedAt,
            ApprovedBy = user.ApprovedBy,
            InvitedBy = user.InvitedBy,
            SuspendedAt = user.SuspendedAt,
            SuspensionReason = user.SuspensionReason,
            RejectionReason = user.RejectionReason
        };
    }

    private static string GenerateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}

