namespace GenericInventory.Reminders.Services;

public sealed class PowerAutomateReminderEffectiveSettings
{
    public string DailyWebhookUrl { get; set; } = string.Empty;
    public string MovementWebhookUrl { get; set; } = string.Empty;
    public string ManualWebhookUrl { get; set; } = string.Empty;
    public string SharedSecret { get; set; } = string.Empty;
}
