using EmailSpamFilter.Infrastructure.Configuracao;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Options;

namespace EmailSpamFilter.Infrastructure.Imap;

public sealed class FabricaClienteImap
{
    private readonly ConfiguracoesImap _config;
    private readonly ProvedorTokenOAuth _provedorToken;

    public FabricaClienteImap(IOptions<ConfiguracoesImap> options, ProvedorTokenOAuth provedorToken)
    {
        _config = options.Value;
        _provedorToken = provedorToken;
    }

    public async Task<ImapClient> CriarAutenticadoAsync(CancellationToken ct)
    {
        // Obtém o token ANTES de conectar — evita timeout da conexão IMAP
        // enquanto o usuário autentica no browser (Device Code Flow)
        var token = await _provedorToken.ObterTokenAsync(ct);

        var client = new ImapClient();
        await client.ConnectAsync(_config.Servidor, _config.Porta, _config.UsarSsl, ct);

        var oauth2 = new SaslMechanismOAuth2(_config.Email, token);
        await client.AuthenticateAsync(oauth2, ct);

        return client;
    }
}
