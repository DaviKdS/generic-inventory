using System.ComponentModel.DataAnnotations;

namespace GenericInventory.Auth.Dtos;

public class ForgotPasswordRequestDto
{
    [Required]
    [EmailAddress]
    [MaxLength(180)]
    public string Email { get; set; } = string.Empty;
}

