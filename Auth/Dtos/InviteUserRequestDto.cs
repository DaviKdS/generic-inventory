using System.ComponentModel.DataAnnotations;
using GenericInventory.Auth.AccessControl;

namespace GenericInventory.Auth.Dtos;

/// <summary>
/// Criação de acesso pelo administrador. Não aceita senha:
/// o convidado recebe um link de uso único por e-mail e define a própria senha.
/// </summary>
public class InviteUserRequestDto
{
    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(180)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = AccessRoleCatalog.DefaultRole;
}

