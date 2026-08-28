namespace GenericInventory.Auth.Dtos;

public class ApprovalDecisionDto
{
    public string Role { get; set; } = "standard";
    public string Reason { get; set; } = string.Empty;
}

