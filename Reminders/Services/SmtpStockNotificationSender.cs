using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using GenericInventory.Auth.Options;

namespace GenericInventory.Reminders.Services;

public class SmtpStockNotificationSender : IStockNotificationSender
{
    private readonly SmtpOptions _smtpOptions;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SmtpStockNotificationSender> _logger;

    public SmtpStockNotificationSender(
        IOptions<SmtpOptions> smtpOptions,
        IWebHostEnvironment environment,
        ILogger<SmtpStockNotificationSender> logger)
    {
        _smtpOptions = smtpOptions.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<StockNotificationResult> SendAsync(
        StockNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var recipients = request.Recipients;
        if (recipients.Count == 0)
        {
            return new StockNotificationResult
            {
                Delivered = false,
                Channel = "none",
                Detail = "Nenhum destinatario configurado."
            };
        }

        if (string.IsNullOrWhiteSpace(_smtpOptions.Host))
        {
            return await WriteFallbackAsync(request, cancellationToken);
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_smtpOptions.From),
            Subject = request.Subject,
            Body = request.Body
        };

        foreach (var recipient in recipients)
        {
            message.To.Add(recipient);
        }

        foreach (var attachment in request.Attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.ContentBytes))
            {
                continue;
            }

            var bytes = Convert.FromBase64String(attachment.ContentBytes);
            message.Attachments.Add(new Attachment(
                new MemoryStream(bytes),
                attachment.Name,
                attachment.ContentType));
        }

        using var client = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_smtpOptions.User))
        {
            client.Credentials = new NetworkCredential(_smtpOptions.User, _smtpOptions.Password);
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);
            return new StockNotificationResult
            {
                Delivered = true,
                Channel = "smtp",
                Detail = $"Enviado para {recipients.Count} destinatario(s)."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stock reminder SMTP delivery failed.");
            return new StockNotificationResult
            {
                Delivered = false,
                Channel = "smtp",
                Detail = $"Falha no envio SMTP: {ex.Message}"
            };
        }
    }

    private async Task<StockNotificationResult> WriteFallbackAsync(
        StockNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var appData = Path.Combine(_environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appData);

        var logPath = Path.Combine(appData, "stock-reminders.log");
        var content = $"""
            === {DateTimeOffset.UtcNow:O} ===
            Trigger: {request.TriggerType}
            To: {string.Join("; ", request.Recipients)}
            Subject: {request.Subject}
            Attachments: {string.Join("; ", request.Attachments.Select(attachment => attachment.Name))}

            {request.Body}

            """;

        await File.AppendAllTextAsync(logPath, content, cancellationToken);
        _logger.LogWarning("Stock reminder notification was written to {LogPath}.", logPath);

        return new StockNotificationResult
        {
            Delivered = false,
            Logged = true,
            Channel = "log",
            Detail = logPath
        };
    }
}
