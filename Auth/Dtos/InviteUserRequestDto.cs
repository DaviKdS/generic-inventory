using System.ComponentModel.DataAnnotations;
using GenericInventory.Auth.AccessControl;

namespace GenericInventory.Auth.Dtos;

/// <summary>
/// Criacao de acesso pelo administrador. Nao aceita senha:
/// o convidado recebe um link de uso unico por e-mail e define a propria senha.
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

