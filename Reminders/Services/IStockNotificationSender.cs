namespace GenericInventory.Reminders.Services;

public interface IStockNotificationSender
{
    Task<StockNotificationResult> SendAsync(
        StockNotificationRequest request,
        CancellationToken cancellationToken = default);
}
