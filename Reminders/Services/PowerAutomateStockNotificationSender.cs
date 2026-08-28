using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using GenericInventory.Auth.Options;

namespace GenericInventory.Reminders.Services;

public class PowerAutomateStockNotificationSender : IStockNotificationSender
{
    private readonly PowerAutomateReminderOptions _options;
    private readonly PowerAutomateReminderSettingsService _settingsService;
    private readonly AppNotificationOptions _appOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SmtpStockNotificationSender _fallbackSender;
    private readonly ILogger<PowerAutomateStockNotificationSender> _logger;

    public PowerAutomateStockNotificationSender(
        IOptions<PowerAutomateReminderOptions> options,
        PowerAutomateReminderSettingsService settingsService,
        IOptions<AppNotificationOptions> appOptions,
        IHttpClientFactory httpClientFactory,
        SmtpStockNotificationSender fallbackSender,
        ILogger<PowerAutomateStockNotificationSender> logger)
    {
        _options = options.Value;
        _settingsService = settingsService;
        _appOptions = appOptions.Value;
        _httpClientFactory = httpClientFactory;
        _fallbackSender = fallbackSender;
        _logger = logger;
    }

    public async Task<StockNotificationResult> SendAsync(
        StockNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Recipients.Count == 0)
        {
            return new StockNotificationResult
            {
                Delivered = false,
                Channel = "none",
                Detail = "Nenhum destinatario configurado."
            };
        }

        var settings = await _settingsService.GetEffectiveAsync(cancellationToken);
        if (TryGetWebhookUri(settings, request.TriggerType, out var webhookUri))
        {
            var result = await TrySendPowerAutomateAsync(webhookUri, settings.SharedSecret, request, cancellationToken);
            if (result.Delivered)
            {
                return result;
            }

            _logger.LogWarning(
                "Power Automate reminder webhook failed for trigger {TriggerType}: {Detail}",
                request.TriggerType,
                result.Detail);
        }

        return await _fallbackSender.SendAsync(request, cancellationToken);
    }

    private async Task<StockNotificationResult> TrySendPowerAutomateAsync(
        Uri webhookUri,
        string sharedSecret,
        StockNotificationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 120)));

            var httpClient = _httpClientFactory.CreateClient(nameof(PowerAutomateStockNotificationSender));
            using var message = new HttpRequestMessage(HttpMethod.Post, webhookUri)
            {
                Content = JsonContent.Create(BuildPayload(request))
            };

            if (!string.IsNullOrWhiteSpace(sharedSecret))
            {
                message.Headers.Add("x-generic-inventory-secret", sharedSecret);
            }

            using var response = await httpClient.SendAsync(message, timeout.Token);
            if (response.IsSuccessStatusCode)
            {
                return new StockNotificationResult
                {
                    Delivered = true,
                    Channel = "power-automate",
                    Detail = $"Flow HTTP aceitou o alerta com status {(int)response.StatusCode}."
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync(timeout.Token);
            return new StockNotificationResult
            {
                Delivered = false,
                Channel = "power-automate",
                Detail = $"Flow retornou HTTP {(int)response.StatusCode}: {responseBody}"
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new StockNotificationResult
            {
                Delivered = false,
                Channel = "power-automate",
                Detail = ex.Message
            };
        }
    }

    private object BuildPayload(StockNotificationRequest request)
    {
        var baseUrl = _appOptions.PublicBaseUrl.Trim().TrimEnd('/');
        var htmlBody = WebUtility.HtmlEncode(request.Body).Replace("\n", "<br>");
        return new
        {
            source = "generic-inventory-stock",
            triggerType = request.TriggerType,
            generatedAt = request.GeneratedAt,
            app = new
            {
                name = "Controle de Estoque",
                url = string.IsNullOrWhiteSpace(baseUrl) ? string.Empty : $"{baseUrl}/"
            },
            rule = new
            {
                request.Rule.Id,
                request.Rule.Name,
                request.Rule.IsActive,
                request.Rule.DailyTime,
                request.Rule.UseProductMinimum,
                request.Rule.ThresholdQuantity,
                request.Rule.ProductCodesCsv,
                request.Rule.TriggerOnMovement,
                request.Rule.IncludeProductImages,
                request.Rule.MaxPhotoAttachments
            },
            message = new
            {
                to = string.Join(";", request.Recipients),
                recipients = request.Recipients,
                request.Subject,
                textBody = request.Body,
                htmlBody,
                attachments = request.Attachments.Select(attachment => new
                {
                    name = attachment.Name,
                    contentType = attachment.ContentType,
                    contentBytes = attachment.ContentBytes
                }).ToList()
            },
            stock = new
            {
                criticalCount = request.Products.Count,
                products = request.Products.Select(product => new
                {
                    product.Code,
                    product.Description,
                    product.CurrentStock,
                    product.MinimumStock,
                    product.SaleValue,
                    product.Catalyst,
                    product.ImagePath,
                    product.LegacyImageUrl,
                    threshold = request.Rule.UseProductMinimum
                        ? product.MinimumStock
                        : request.Rule.ThresholdQuantity ?? product.MinimumStock,
                    deficit = Math.Max(0, (request.Rule.UseProductMinimum
                        ? product.MinimumStock
                        : request.Rule.ThresholdQuantity ?? product.MinimumStock) - product.CurrentStock)
                }).ToList()
            }
        };
    }

    private static bool TryGetWebhookUri(
        PowerAutomateReminderEffectiveSettings settings,
        string triggerType,
        out Uri webhookUri)
    {
        var configuredUrl = triggerType switch
        {
            ReminderTriggerTypes.Daily => settings.DailyWebhookUrl,
            ReminderTriggerTypes.Movement => settings.MovementWebhookUrl,
            ReminderTriggerTypes.Manual => settings.ManualWebhookUrl,
            _ => string.Empty
        };

        return Uri.TryCreate(configuredUrl, UriKind.Absolute, out webhookUri!) &&
            (webhookUri.Scheme == Uri.UriSchemeHttps || webhookUri.Scheme == Uri.UriSchemeHttp);
    }
}
