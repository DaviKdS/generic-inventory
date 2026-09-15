using System.ComponentModel.DataAnnotations;

namespace GenericInventory.Auth.Dtos;

public class DeveloperPasswordRequestDto
{
    [Required]
    [MinLength(8)]
    [MaxLength(200)]
    public string Password { get; set; } = string.Empty;
}
