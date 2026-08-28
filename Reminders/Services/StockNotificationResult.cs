namespace GenericInventory.Reminders.Services;

public class StockNotificationResult
{
    public bool Delivered { get; set; }
    public bool Logged { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
