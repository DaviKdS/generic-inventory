namespace GenericInventory.Auth.Options;

/// <summary>
/// Conta administradora semeada na subida da aplicacao.
/// Não existe senha aqui: a conta nasce sem senha e recebe por e-mail um link de uso único
/// para o próprio administrador definir a dele.
/// </summary>
public class AccessBootstrapOptions
{
    public bool Enabled { get; set; } = true;

    public string Name { get; set; } = "Administrador";

    /// <summary>Quando vazio, cai no Auth:ApproverEmail.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Senha inicial ja em forma de hash, no formato v1:iteracoes:salt:hash gravado por
    /// <see cref="Services.PasswordHashingService"/>. Nunca aceita senha em texto puro.
    /// Serve para provisionar o acesso quando o link por e-mail não é viável; deve vir do
    /// cofre de segredos do ambiente, nunca do repositorio.
    /// Só é aplicada quando a conta ainda está sem senha: nunca sobrescreve uma existente.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
}

