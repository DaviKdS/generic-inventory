using System.ComponentModel.DataAnnotations;

namespace GenericInventory.Auth.Dtos;

public class AccessStatusRequestDto
{
    [MaxLength(300)]
    public string Reason { get; set; } = string.Empty;
}

