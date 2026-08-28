namespace GenericInventory.Reminders.Dtos;

public class PowerAutomateReminderSettingsDto
{
    public bool PowerAutomateConfigured { get; set; }
    public bool DailyConfigured { get; set; }
    public bool MovementConfigured { get; set; }
    public bool ManualConfigured { get; set; }
    public bool SharedSecretConfigured { get; set; }
    public string DailyWebhookUrlPreview { get; set; } = string.Empty;
    public string MovementWebhookUrlPreview { get; set; } = string.Empty;
    public string ManualWebhookUrlPreview { get; set; } = string.Empty;
    public string DailySource { get; set; } = string.Empty;
    public string MovementSource { get; set; } = string.Empty;
    public string ManualSource { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

public class PowerAutomateReminderSettingsFormDto
{
    public string? DailyWebhookUrl { get; set; }
    public string? MovementWebhookUrl { get; set; }
    public string? ManualWebhookUrl { get; set; }
    public string? SharedSecret { get; set; }
    public bool ClearDailyWebhookUrl { get; set; }
    public bool ClearMovementWebhookUrl { get; set; }
    public bool ClearManualWebhookUrl { get; set; }
    public bool ClearSharedSecret { get; set; }
}
