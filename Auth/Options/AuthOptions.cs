namespace GenericInventory.Auth.Options;

public class AuthOptions
{
    /// <summary>Caixa que recebe as solicitacoes de acesso.</summary>
    public string ApproverEmail { get; set; } = "luizds979@gmail.com";

    public string StorePath { get; set; } = "App_Data/auth-users.json";
    public string CookieName { get; set; } = "generic-inventory.auth";
    public int SessionHours { get; set; } = 8;
    public int ApprovalTokenHours { get; set; } = 72;

    /// <summary>Validade do link de definicao/redefinicao de senha enviado por e-mail.</summary>
    public int PasswordTokenHours { get; set; } = 24;

    /// <summary>
    /// Permite que um visitante abra uma solicitacao de acesso. Mesmo habilitado,
    /// a solicitacao nasce sem permissao: quem distribui o acesso e o administrador.
    /// </summary>
    public bool AllowSelfRegistration { get; set; } = true;

    public AccessBootstrapOptions Bootstrap { get; set; } = new();
}

