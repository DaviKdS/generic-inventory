namespace GenericInventory.Reminders.Dtos;

public class ReminderSendResultDto
{
    public int RuleId { get; set; }
    public int CriticalProducts { get; set; }
    public bool Sent { get; set; }
    public bool Logged { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
