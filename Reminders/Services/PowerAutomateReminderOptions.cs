namespace GenericInventory.Reminders.Services;

public class PowerAutomateReminderOptions
{
    public string DailyWebhookUrl { get; set; } = string.Empty;
    public string MovementWebhookUrl { get; set; } = string.Empty;
    public string ManualWebhookUrl { get; set; } = string.Empty;
    public string SharedSecret { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 20;
}
