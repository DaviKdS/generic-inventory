namespace GenericInventory.Reminders.Entities;

public class ReminderRule
{
    public int Id { get; set; }
    public string Name { get; set; } = "Alerta de estoque baixo";
    public bool IsActive { get; set; } = true;
    public string DailyTime { get; set; } = "08:00";
    public string Recipients { get; set; } = string.Empty;
    public string Subject { get; set; } = "Alerta de Estoque Baixo";
    public string MessageTemplate { get; set; } = StockReminderDefaults.MessageTemplate;
    public bool UseProductMinimum { get; set; } = true;
    public decimal? ThresholdQuantity { get; set; }
    public string ProductCodesCsv { get; set; } = string.Empty;
    public bool TriggerOnMovement { get; set; } = true;
    public bool IncludeProductImages { get; set; }
    public int MaxPhotoAttachments { get; set; } = 3;
    public DateTimeOffset? LastDailyRunAt { get; set; }
}

public static class StockReminderDefaults
{
    public const string MessageTemplate = """
        Estoque baixo

        {Products}

        Gerado em: {GeneratedAt}
        """;
}
