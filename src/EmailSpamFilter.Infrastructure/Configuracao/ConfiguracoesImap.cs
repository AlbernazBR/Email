namespace EmailSpamFilter.Infrastructure.Configuracao;

public sealed class ConfiguracoesImap
{
    public string Servidor { get; set; } = "outlook.office365.com";
    public int Porta { get; set; } = 993;
    public bool UsarSsl { get; set; } = true;

    /// <summary>
    /// Endereço de e-mail da conta. Defina via user-secrets ou variável de ambiente.
    /// NUNCA commite no Git.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Client ID do app registrado no Microsoft Entra ID (portal.azure.com).
    /// Defina via user-secrets ou variável de ambiente ConfiguracoesImap__ClientId.
    /// NUNCA commite no Git.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
}
