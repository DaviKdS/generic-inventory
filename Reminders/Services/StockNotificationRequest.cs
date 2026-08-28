using GenericInventory.Products.Entities;
using GenericInventory.Reminders.Entities;

namespace GenericInventory.Reminders.Services;

public class StockNotificationRequest
{
    public string TriggerType { get; set; } = ReminderTriggerTypes.Manual;
    public ReminderRule Rule { get; set; } = new();
    public IReadOnlyList<Product> Products { get; set; } = Array.Empty<Product>();
    public IReadOnlyList<string> Recipients { get; set; } = Array.Empty<string>();
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public IReadOnlyList<StockNotificationAttachment> Attachments { get; set; } = Array.Empty<StockNotificationAttachment>();
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.Now;
}
