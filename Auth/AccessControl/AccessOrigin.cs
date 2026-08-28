namespace GenericInventory.Auth.AccessControl;

/// <summary>
/// Como o acesso entrou no sistema. Serve para auditoria no painel de acessos.
/// </summary>
public static class AccessOrigin
{
    /// <summary>Conta administradora semeada pela configuracao do ambiente.</summary>
    public const string Bootstrap = "bootstrap";

    /// <summary>Conta criada pelo administrador no painel de acessos.</summary>
    public const string Invite = "invite";

    /// <summary>Solicitacao aberta pelo proprio usuario na tela de cadastro.</summary>
    public const string SelfService = "self-service";
}

