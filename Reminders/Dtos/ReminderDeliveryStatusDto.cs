namespace GenericInventory.Reminders.Dtos;

public class ReminderDeliveryStatusDto
{
    public bool PowerAutomateConfigured { get; set; }
    public bool PowerAutomateDailyConfigured { get; set; }
    public bool PowerAutomateMovementConfigured { get; set; }
    public bool PowerAutomateManualConfigured { get; set; }
    public bool SmtpConfigured { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string FallbackPath { get; set; } = "App_Data/stock-reminders.log";
}
