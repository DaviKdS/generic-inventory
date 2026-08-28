using System.Text.Json;
using Microsoft.Extensions.Options;
using GenericInventory.Auth.Dtos;
using GenericInventory.Auth.Options;

namespace GenericInventory.Auth.Services;

public class ApprovalFlowSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ApprovalFlowOptions _options;
    private readonly string _storePath;

    public ApprovalFlowSettingsService(
        IOptions<ApprovalFlowOptions> options,
        IWebHostEnvironment environment)
    {
        _options = options.Value;
        _storePath = Path.IsPathRooted(_options.SettingsStorePath)
            ? _options.SettingsStorePath
            : Path.Combine(environment.ContentRootPath, _options.SettingsStorePath);
    }

    public async Task<ApprovalFlowSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var stored = await ReadAsync(cancellationToken);
        return BuildDto(stored);
    }

    public async Task<string> GetEffectiveWebhookUrlAsync(CancellationToken cancellationToken = default)
    {
        var stored = await ReadAsync(cancellationToken);
        return Effective(stored?.WebhookUrl, _options.PowerAutomateWebhookUrl);
    }

    public async Task<ApprovalFlowSettingsDto> SaveAsync(
        ApprovalFlowSettingsFormDto form,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var stored = await ReadAsync(cancellationToken) ?? new ApprovalFlowSettingsRecord();
        if (!string.IsNullOrWhiteSpace(form.WebhookUrl))
        {
            stored.WebhookUrl = NormalizeWebhookUrl(form.WebhookUrl, "solicitacao de acesso");
        }
        else if (form.ClearWebhookUrl)
        {
            stored.WebhookUrl = string.Empty;
        }

        stored.UpdatedAt = DateTimeOffset.UtcNow;
        stored.UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "admin" : updatedBy.Trim();
        await WriteAsync(stored, cancellationToken);

        return BuildDto(stored);
    }

    private async Task<ApprovalFlowSettingsRecord?> ReadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_storePath))
            {
                return null;
            }

            await using var stream = File.OpenRead(_storePath);
            return await JsonSerializer.DeserializeAsync<ApprovalFlowSettingsRecord>(stream, JsonOptions, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WriteAsync(ApprovalFlowSettingsRecord stored, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_storePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(_storePath);
            await JsonSerializer.SerializeAsync(stream, stored, JsonOptions, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private ApprovalFlowSettingsDto BuildDto(ApprovalFlowSettingsRecord? stored)
    {
        var webhookUrl = Effective(stored?.WebhookUrl, _options.PowerAutomateWebhookUrl);
        var configured = IsWebhookConfigured(webhookUrl);
        return new ApprovalFlowSettingsDto
        {
            Configured = configured,
            WebhookUrlPreview = MaskUrl(webhookUrl),
            Source = Source(stored?.WebhookUrl, _options.PowerAutomateWebhookUrl),
            UpdatedAt = stored?.UpdatedAt,
            UpdatedBy = stored?.UpdatedBy ?? string.Empty
        };
    }

    private static string NormalizeWebhookUrl(string value, string label)
    {
        var trimmed = value.Trim();
        if (!IsWebhookConfigured(trimmed))
        {
            throw new InvalidOperationException($"URL do Power Automate para {label} invalida.");
        }

        return trimmed;
    }

    private static bool IsWebhookConfigured(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    private static string Effective(string? storedValue, string configuredValue)
    {
        return !string.IsNullOrWhiteSpace(storedValue)
            ? storedValue.Trim()
            : configuredValue.Trim();
    }

    private static string Source(string? storedValue, string configuredValue)
    {
        if (!string.IsNullOrWhiteSpace(storedValue))
        {
            return "site";
        }

        return IsWebhookConfigured(configuredValue) ? "environment" : string.Empty;
    }

    private static string MaskUrl(string value)
    {
        if (!IsWebhookConfigured(value))
        {
            return string.Empty;
        }

        var uri = new Uri(value);
        var path = uri.AbsolutePath;
        var tail = path.Length <= 14 ? path : path[^14..];
        return $"{uri.Scheme}://{uri.Host}/...{tail}";
    }

    private sealed class ApprovalFlowSettingsRecord
    {
        public string WebhookUrl { get; set; } = string.Empty;
        public DateTimeOffset? UpdatedAt { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
    }
}
