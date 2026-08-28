namespace GenericInventory.Auth.AccessControl;

/// <summary>
/// Motivo do envio do link de senha. Chega ao fluxo do Power Automate para escolher o texto do e-mail.
/// </summary>
public static class PasswordSetupReason
{
    public const string Bootstrap = "bootstrap";
    public const string Invite = "invite";
    public const string Reset = "reset";
}

