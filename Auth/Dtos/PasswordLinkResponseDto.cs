namespace GenericInventory.Auth.Dtos;

public class PasswordLinkResponseDto
{
    public UserAccessDto User { get; set; } = new();
    public string PasswordSetupUrl { get; set; } = string.Empty;
}
