using System.ComponentModel.DataAnnotations;
using GenericInventory.Auth.AccessControl;

namespace GenericInventory.Auth.Dtos;

public class ChangeRoleRequestDto
{
    [Required]
    public string Role { get; set; } = AccessRoleCatalog.DefaultRole;
}

