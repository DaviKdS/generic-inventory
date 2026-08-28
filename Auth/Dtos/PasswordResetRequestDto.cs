using System.ComponentModel.DataAnnotations;

namespace GenericInventory.Auth.Dtos;

public class PasswordResetRequestDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(200)]
    public string Password { get; set; } = string.Empty;
}

