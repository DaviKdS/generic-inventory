namespace GenericInventory.Reminders.Dtos;

public class ReminderRuleFormDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string DailyTime { get; set; } = "08:00";
    public string Recipients { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
    public bool UseProductMinimum { get; set; } = true;
    public decimal? ThresholdQuantity { get; set; }
    public string ProductCodesCsv { get; set; } = string.Empty;
    public bool TriggerOnMovement { get; set; } = true;
    public bool IncludeProductImages { get; set; }
    public int MaxPhotoAttachments { get; set; } = 3;
}
