namespace GenericInventory.Auth.Dtos;

public class ApprovalFlowSettingsDto
{
    public bool Configured { get; set; }
    public string WebhookUrlPreview { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

public class ApprovalFlowSettingsFormDto
{
    public string? WebhookUrl { get; set; }
    public bool ClearWebhookUrl { get; set; }
}
