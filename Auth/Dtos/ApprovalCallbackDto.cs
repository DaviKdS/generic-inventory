namespace GenericInventory.Auth.Dtos;

public class ApprovalCallbackDto
{
    public string Token { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string Role { get; set; } = "standard";
    public string Reason { get; set; } = string.Empty;
    public string DecidedBy { get; set; } = string.Empty;
}

