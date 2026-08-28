namespace GenericInventory.Auth.Dtos;

/// <summary>
/// Filtros da listagem de acessos no painel do administrador.
/// </summary>
public class UserQueryDto
{
    public string? Status { get; set; }
    public string? Role { get; set; }

    /// <summary>Busca por nome ou e-mail.</summary>
    public string? Search { get; set; }
}

