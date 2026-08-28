using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using GenericInventory.Data;
using GenericInventory.Reminders.Dtos;
using GenericInventory.Reminders.Entities;

namespace GenericInventory.Reminders.Services;

public class PowerAutomateReminderSettingsService
{
    private const int SettingsId = 1;
    private readonly AppDbContext _db;
    private readonly PowerAutomateReminderOptions _options;

    public PowerAutomateReminderSettingsService(
        AppDbContext db,
        IOptions<PowerAutomateReminderOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<PowerAutomateReminderSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var stored = await GetStoredAsync(cancellationToken);
        return BuildDto(stored);
    }

    public async Task<PowerAutomateReminderEffectiveSettings> GetEffectiveAsync(CancellationToken cancellationToken = default)
    {
        var stored = await GetStoredAsync(cancellationToken);
        return new PowerAutomateReminderEffectiveSettings
        {
            DailyWebhookUrl = Effective(stored?.DailyWebhookUrl, _options.DailyWebhookUrl),
            MovementWebhookUrl = Effective(stored?.MovementWebhookUrl, _options.MovementWebhookUrl),
            ManualWebhookUrl = Effective(stored?.ManualWebhookUrl, _options.ManualWebhookUrl),
            SharedSecret = Effective(stored?.SharedSecret, _options.SharedSecret)
        };
    }

    public async Task<PowerAutomateReminderSettingsDto> SaveAsync(
        PowerAutomateReminderSettingsFormDto form,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var stored = await GetOrCreateAsync(cancellationToken);

        ApplyWebhook(
            form.DailyWebhookUrl,
            form.ClearDailyWebhookUrl,
            "diario",
            value => stored.DailyWebhookUrl = value);
        ApplyWebhook(
            form.MovementWebhookUrl,
            form.ClearMovementWebhookUrl,
            "movimentacao",
            value => stored.MovementWebhookUrl = value);
        ApplyWebhook(
            form.ManualWebhookUrl,
            form.ClearManualWebhookUrl,
            "teste manual",
            value => stored.ManualWebhookUrl = value);

        if (!string.IsNullOrWhiteSpace(form.SharedSecret))
        {
            stored.SharedSecret = form.SharedSecret.Trim();
        }
        else if (form.ClearSharedSecret)
        {
            stored.SharedSecret = string.Empty;
        }

        stored.UpdatedAt = DateTimeOffset.UtcNow;
        stored.UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "admin" : updatedBy.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        return BuildDto(stored);
    }

    public async Task<bool> SaveWebhookUrlAsync(
        string triggerType,
        string webhookUrl,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var normalizedUrl = NormalizeWebhookUrl(webhookUrl, triggerType);
        var stored = await GetOrCreateAsync(cancellationToken);

        switch (triggerType)
        {
            case ReminderTriggerTypes.Daily:
                stored.DailyWebhookUrl = normalizedUrl;
                break;
            case ReminderTriggerTypes.Movement:
                stored.MovementWebhookUrl = normalizedUrl;
                break;
            case ReminderTriggerTypes.Manual:
                stored.ManualWebhookUrl = normalizedUrl;
                break;
            default:
                return false;
        }

        stored.UpdatedAt = DateTimeOffset.UtcNow;
        stored.UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "admin" : updatedBy.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<PowerAutomateReminderSettings?> GetStoredAsync(CancellationToken cancellationToken)
    {
        return await _db.PowerAutomateReminderSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(settings => settings.Id == SettingsId, cancellationToken);
    }

    private async Task<PowerAutomateReminderSettings> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var stored = await _db.PowerAutomateReminderSettings
            .FirstOrDefaultAsync(settings => settings.Id == SettingsId, cancellationToken);
        if (stored != null)
        {
            return stored;
        }

        stored = new PowerAutomateReminderSettings { Id = SettingsId };
        _db.PowerAutomateReminderSettings.Add(stored);
        return stored;
    }

    private PowerAutomateReminderSettingsDto BuildDto(PowerAutomateReminderSettings? stored)
    {
        var dailyUrl = Effective(stored?.DailyWebhookUrl, _options.DailyWebhookUrl);
        var movementUrl = Effective(stored?.MovementWebhookUrl, _options.MovementWebhookUrl);
        var manualUrl = Effective(stored?.ManualWebhookUrl, _options.ManualWebhookUrl);
        var dailyConfigured = IsWebhookConfigured(dailyUrl);
        var movementConfigured = IsWebhookConfigured(movementUrl);
        var manualConfigured = IsWebhookConfigured(manualUrl);

        return new PowerAutomateReminderSettingsDto
        {
            PowerAutomateConfigured = dailyConfigured || movementConfigured || manualConfigured,
            DailyConfigured = dailyConfigured,
            MovementConfigured = movementConfigured,
            ManualConfigured = manualConfigured,
            SharedSecretConfigured = !string.IsNullOrWhiteSpace(Effective(stored?.SharedSecret, _options.SharedSecret)),
            DailyWebhookUrlPreview = MaskUrl(dailyUrl),
            MovementWebhookUrlPreview = MaskUrl(movementUrl),
            ManualWebhookUrlPreview = MaskUrl(manualUrl),
            DailySource = Source(stored?.DailyWebhookUrl, _options.DailyWebhookUrl),
            MovementSource = Source(stored?.MovementWebhookUrl, _options.MovementWebhookUrl),
            ManualSource = Source(stored?.ManualWebhookUrl, _options.ManualWebhookUrl),
            UpdatedAt = stored?.UpdatedAt,
            UpdatedBy = stored?.UpdatedBy ?? string.Empty
        };
    }

    private static void ApplyWebhook(
        string? value,
        bool clear,
        string label,
        Action<string> assign)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            assign(NormalizeWebhookUrl(value, label));
            return;
        }

        if (clear)
        {
            assign(string.Empty);
        }
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
}
