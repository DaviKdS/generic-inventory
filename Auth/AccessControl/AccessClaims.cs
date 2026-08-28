namespace GenericInventory.Auth.AccessControl;

/// <summary>
/// Claims proprias gravadas no cookie de sessao.
/// </summary>
public static class AccessClaims
{
    /// <summary>Status do acesso no momento do login.</summary>
    public const string Status = "access_status";

    /// <summary>
    /// Carimbo de seguranca. Muda sempre que papel, status ou senha mudam,
    /// permitindo derrubar sessoes antigas sem esperar o cookie expirar.
    /// </summary>
    public const string Stamp = "access_stamp";
}

