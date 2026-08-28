namespace GenericInventory.Auth.Options;

public class ApprovalFlowOptions
{
    public string PowerAutomateWebhookUrl { get; set; } = string.Empty;
    public string SettingsStorePath { get; set; } = "App_Data/approval-flow-settings.json";
}

