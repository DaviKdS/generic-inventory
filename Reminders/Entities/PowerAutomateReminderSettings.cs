namespace GenericInventory.Reminders.Entities;

public class PowerAutomateReminderSettings
{
    public int Id { get; set; } = 1;
    public string DailyWebhookUrl { get; set; } = string.Empty;
    public string MovementWebhookUrl { get; set; } = string.Empty;
    public string ManualWebhookUrl { get; set; } = string.Empty;
    public string SharedSecret { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}
