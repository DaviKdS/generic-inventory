namespace GenericInventory.Reminders.Services;

public class StockReminderOptions
{
    public bool SchedulerEnabled { get; set; } = true;
    public int PollSeconds { get; set; } = 60;
}
