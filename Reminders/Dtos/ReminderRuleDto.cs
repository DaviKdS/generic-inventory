using GenericInventory.Reminders.Entities;

namespace GenericInventory.Reminders.Dtos;

public class ReminderRuleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string DailyTime { get; set; } = string.Empty;
    public string Recipients { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
    public bool UseProductMinimum { get; set; }
    public decimal? ThresholdQuantity { get; set; }
    public string ProductCodesCsv { get; set; } = string.Empty;
    public bool TriggerOnMovement { get; set; }
    public bool IncludeProductImages { get; set; }
    public int MaxPhotoAttachments { get; set; } = 3;
    public DateTimeOffset? LastDailyRunAt { get; set; }

    public static ReminderRuleDto FromEntity(ReminderRule rule)
    {
        return new ReminderRuleDto
        {
            Id = rule.Id,
            Name = rule.Name,
            IsActive = rule.IsActive,
            DailyTime = rule.DailyTime,
            Recipients = rule.Recipients,
            Subject = rule.Subject,
            MessageTemplate = rule.MessageTemplate,
            UseProductMinimum = rule.UseProductMinimum,
            ThresholdQuantity = rule.ThresholdQuantity,
            ProductCodesCsv = rule.ProductCodesCsv,
            TriggerOnMovement = rule.TriggerOnMovement,
            IncludeProductImages = rule.IncludeProductImages,
            MaxPhotoAttachments = rule.MaxPhotoAttachments,
            LastDailyRunAt = rule.LastDailyRunAt
        };
    }
}
