using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using GenericInventory.Auth.AccessControl;
using GenericInventory.Auth.Entities;
using GenericInventory.Auth.Interfaces;
using GenericInventory.Auth.Options;

namespace GenericInventory.Auth.Services;

public class ApprovalNotificationSender : IApprovalNotifier
{
    private readonly AuthOptions _authOptions;
    private readonly SmtpOptions _smtpOptions;
    private readonly AppNotificationOptions _appOptions;
    private readonly ApprovalFlowSettingsService _approvalFlowSettingsService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ApprovalNotificationSender> _logger;

    public ApprovalNotificationSender(
        IOptions<AuthOptions> authOptions,
        IOptions<SmtpOptions> smtpOptions,
        IOptions<AppNotificationOptions> appOptions,
        ApprovalFlowSettingsService approvalFlowSettingsService,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment environment,
        ILogger<ApprovalNotificationSender> logger)
    {
        _authOptions = authOptions.Value;
        _smtpOptions = smtpOptions.Value;
        _appOptions = appOptions.Value;
        _approvalFlowSettingsService = approvalFlowSettingsService;
        _httpClientFactory = httpClientFactory;
        _environment = environment;
        _logger = logger;
    }

    public async Task SendApprovalRequestAsync(UserAccessRecord user, string approvalToken, CancellationToken cancellationToken = default)
    {
        var subject = "Controle de Estoque - novo cadastro pendente";
        var body = $"""
            Novo cadastro aguardando aprovacao.

            Nome: {user.Name}
            E-mail: {user.Email}
            Criado em UTC: {user.CreatedAt:O}

            Painel de aprovacoes: {BuildPanelUrl(user.Id)}
            """;

        if (await TrySendPowerAutomateAsync("access.requested", user, approvalToken, subject, body, cancellationToken))
        {
            return;
        }

        await SendFallbackEmailAsync(_authOptions.ApproverEmail, subject, body, cancellationToken);
    }

    public async Task SendAccessApprovedAsync(UserAccessRecord user, CancellationToken cancellationToken = default)
    {
        var subject = "Controle de Estoque - acesso aprovado";
        var body = $"""
            Ola, {user.Name}.

            Seu acesso ao Controle de Estoque foi aprovado.
            Acesse: {BuildAccessUrl()}
            """;

        if (await TrySendPowerAutomateAsync("access.approved", user, string.Empty, subject, body, cancellationToken))
        {
            return;
        }

        await SendFallbackEmailAsync(user.Email, subject, body, cancellationToken);
    }

    public async Task SendAccessRejectedAsync(UserAccessRecord user, CancellationToken cancellationToken = default)
    {
        var reason = string.IsNullOrWhiteSpace(user.RejectionReason)
            ? "Sem observacao adicional."
            : user.RejectionReason;
        var subject = "Controle de Estoque - cadastro recusado";
        var body = $"""
            Ola, {user.Name}.

            Seu pedido de acesso ao Controle de Estoque foi recusado.
            Motivo: {reason}
            """;

        if (await TrySendPowerAutomateAsync("access.rejected", user, string.Empty, subject, body, cancellationToken))
        {
            return;
        }

        await SendFallbackEmailAsync(user.Email, subject, body, cancellationToken);
    }

    public async Task SendPasswordSetupAsync(UserAccessRecord user, string passwordToken, string reason, CancellationToken cancellationToken = default)
    {
        var passwordUrl = BuildPasswordUrl(user.Id, passwordToken);
        var intro = reason switch
        {
            PasswordSetupReason.Bootstrap => "Sua conta administradora do Controle de Estoque esta pronta.",
            PasswordSetupReason.Invite => "Um administrador liberou seu acesso ao Controle de Estoque.",
            _ => "Recebemos um pedido para redefinir sua senha do Controle de Estoque."
        };

        var subject = reason == PasswordSetupReason.Reset
            ? "Controle de Estoque - redefinicao de senha"
            : "Controle de Estoque - defina sua senha";

        var body = $"""
            Ola, {user.Name}.

            {intro}
            Perfil de acesso: {AccessRoleCatalog.Find(user.Role)?.Label ?? user.Role}

            Defina sua senha por este link de uso unico:
            {passwordUrl}

            O link vale por {Math.Max(1, _authOptions.PasswordTokenHours)} hora(s). Se voce nao pediu, ignore esta mensagem.
            """;

        if (await TrySendPowerAutomateAsync($"access.password.{reason}", user, string.Empty, subject, body, cancellationToken, passwordUrl))
        {
            return;
        }

        await SendFallbackEmailAsync(user.Email, subject, body, cancellationToken);
    }

    public async Task SendRoleChangedAsync(UserAccessRecord user, string previousRole, CancellationToken cancellationToken = default)
    {
        var role = AccessRoleCatalog.Find(user.Role);
        var subject = "Controle de Estoque - perfil de acesso atualizado";
        var body = $"""
            Ola, {user.Name}.

            Seu perfil no Controle de Estoque mudou de {previousRole} para {user.Role}.
            {role?.Description ?? string.Empty}

            Acesse: {BuildAccessUrl()}
            """;

        if (await TrySendPowerAutomateAsync("access.role.changed", user, string.Empty, subject, body, cancellationToken))
        {
            return;
        }

        await SendFallbackEmailAsync(user.Email, subject, body, cancellationToken);
    }

    public async Task SendAccessChangedAsync(UserAccessRecord user, string action, CancellationToken cancellationToken = default)
    {
        var (subject, message) = action switch
        {
            "suspended" => ("Controle de Estoque - acesso suspenso",
                $"Seu acesso foi suspenso pelo administrador.{FormatReason(user.SuspensionReason)}"),
            "reactivated" => ("Controle de Estoque - acesso reativado",
                $"Seu acesso foi reativado. Acesse: {BuildAccessUrl()}"),
            _ => ("Controle de Estoque - acesso removido",
                "Seu acesso foi removido pelo administrador.")
        };

        var body = $"""
            Ola, {user.Name}.

            {message}
            """;

        if (await TrySendPowerAutomateAsync($"access.{action}", user, string.Empty, subject, body, cancellationToken))
        {
            return;
        }

        await SendFallbackEmailAsync(user.Email, subject, body, cancellationToken);
    }

    private static string FormatReason(string reason)
    {
        return string.IsNullOrWhiteSpace(reason) ? string.Empty : $"\nMotivo: {reason}";
    }

    private async Task<bool> TrySendPowerAutomateAsync(
        string eventType,
        UserAccessRecord user,
        string approvalToken,
        string subject,
        string body,
        CancellationToken cancellationToken,
        string passwordSetupUrl = "")
    {
        var configuredWebhookUrl = await _approvalFlowSettingsService.GetEffectiveWebhookUrlAsync(cancellationToken);
        if (!Uri.TryCreate(configuredWebhookUrl, UriKind.Absolute, out var webhookUri) ||
            (webhookUri.Scheme != Uri.UriSchemeHttps && webhookUri.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        var callbackUrl = BuildCallbackUrl(user.Id);
        var payload = new
        {
            eventType,
            source = "generic-inventory-auth",
            channel = "teams-outlook",
            approverEmail = _authOptions.ApproverEmail,
            appUrl = BuildAccessUrl(),
            approvalPanelUrl = BuildPanelUrl(user.Id),
            passwordSetupUrl,
            roleLabel = AccessRoleCatalog.Find(user.Role)?.Label ?? user.Role,
            user = new
            {
                user.Id,
                user.Name,
                user.Email,
                user.Role,
                user.Status,
                user.CreatedAt,
                user.ApprovedAt,
                user.ApprovedBy,
                user.RejectedAt,
                user.RejectedBy,
                user.RejectionReason
            },
            message = new
            {
                subject,
                body,
                to = eventType == "access.requested" ? _authOptions.ApproverEmail : user.Email,
                htmlBody = WebUtility.HtmlEncode(body).Replace("\n", "<br>")
            },
            callback = new
            {
                method = "POST",
                url = callbackUrl,
                token = approvalToken,
                approveBody = new
                {
                    token = approvalToken,
                    decision = "approve",
                    role = AccessRoleCatalog.DefaultRole,
                    decidedBy = _authOptions.ApproverEmail
                },
                rejectBody = new
                {
                    token = approvalToken,
                    decision = "reject",
                    reason = "Recusado pelo aprovador",
                    decidedBy = _authOptions.ApproverEmail
                }
            }
        };

        try
        {
            var httpClient = _httpClientFactory.CreateClient(nameof(ApprovalNotificationSender));
            using var response = await httpClient.PostAsJsonAsync(webhookUri, payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Approval Power Automate webhook returned {StatusCode}: {ResponseBody}",
                (int)response.StatusCode,
                responseBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Approval Power Automate webhook failed. Falling back to e-mail/log.");
            return false;
        }
    }

    private async Task SendFallbackEmailAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_smtpOptions.Host))
        {
            await WriteFallbackEmailAsync(to, subject, body, cancellationToken);
            return;
        }

        using var message = new MailMessage(_smtpOptions.From, to, subject, body);
        using var client = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_smtpOptions.User))
        {
            client.Credentials = new NetworkCredential(_smtpOptions.User, _smtpOptions.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }

    private async Task WriteFallbackEmailAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        var appData = Path.Combine(_environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appData);

        var logPath = Path.Combine(appData, "auth-emails.log");
        var content = $"""
            === {DateTimeOffset.UtcNow:O} ===
            To: {to}
            Subject: {subject}

            {body}

            """;

        await File.AppendAllTextAsync(logPath, content, cancellationToken);
        _logger.LogWarning("Approval notification was written to {LogPath}.", logPath);
    }

    private string BuildCallbackUrl(string userId)
    {
        var baseUrl = _appOptions.PublicBaseUrl.Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl)
            ? string.Empty
            : $"{baseUrl}/api/auth/approvals/{Uri.EscapeDataString(userId)}/decision";
    }

    /// <summary>Link de uso unico para a tela de definicao de senha. So trafega por e-mail.</summary>
    private string BuildPasswordUrl(string userId, string passwordToken)
    {
        if (string.IsNullOrWhiteSpace(passwordToken))
        {
            return string.Empty;
        }

        var baseUrl = _appOptions.PublicBaseUrl.Trim().TrimEnd('/');
        var path = $"?view=password&uid={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(passwordToken)}";
        return string.IsNullOrWhiteSpace(baseUrl) ? path : $"{baseUrl}/{path}";
    }

    private string BuildPanelUrl(string userId)
    {
        var baseUrl = _appOptions.PublicBaseUrl.Trim().TrimEnd('/');
        var path = $"?view=access&approval={Uri.EscapeDataString(userId)}";
        return string.IsNullOrWhiteSpace(baseUrl) ? path : $"{baseUrl}/{path}";
    }

    private string BuildAccessUrl()
    {
        var baseUrl = _appOptions.PublicBaseUrl.Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? "/" : $"{baseUrl}/";
    }
}

