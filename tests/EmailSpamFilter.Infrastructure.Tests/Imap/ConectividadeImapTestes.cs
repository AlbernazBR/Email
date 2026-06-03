using EmailSpamFilter.Infrastructure.Configuracao;
using EmailSpamFilter.Infrastructure.Imap;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EmailSpamFilter.Infrastructure.Tests.Imap;

/// <summary>
/// Teste de integração — conecta de verdade ao Outlook IMAP via OAuth2.
///
/// Configure uma única vez:
///   dotnet user-secrets --project src/EmailSpamFilter.Worker set "ConfiguracoesImap:Email" "seuemail@hotmail.com"
///   dotnet user-secrets --project src/EmailSpamFilter.Worker set "ConfiguracoesImap:ClientId" "seu-client-id-do-azure"
///
/// Na primeira execução o console exibirá um código para autenticar no browser.
/// Nas execuções seguintes o token é reutilizado automaticamente.
///
/// Para rodar:
///   dotnet test --filter "Category=Integracao"
/// </summary>
public sealed class ConectividadeImapTestes
{
    private static ConfiguracoesImap? ObterCredenciais()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<ConectividadeImapTestes>()
            .AddEnvironmentVariables()
            .Build();

        var secao = config.GetSection("ConfiguracoesImap");
        var email = secao["Email"];
        var clientId = secao["ClientId"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(clientId))
            return null;

        return new ConfiguracoesImap
        {
            Servidor = secao["Servidor"] ?? "outlook.office365.com",
            Porta = int.TryParse(secao["Porta"], out var porta) ? porta : 993,
            UsarSsl = true,
            Email = email,
            ClientId = clientId
        };
    }

    [Fact]
    [Trait("Category", "Integracao")]
    public async Task Conectar_DeveAutenticar_QuandoCredenciaisCorretas()
    {
        var config = ObterCredenciais();
        if (config is null)
        {
            // Sem credenciais → pula silenciosamente
            return;
        }

        var opcoes = Options.Create(config);
        var provedor = new ProvedorTokenOAuth(opcoes, NullLogger<ProvedorTokenOAuth>.Instance);
        var fabrica = new FabricaClienteImap(opcoes, provedor);

        using var cliente = await fabrica.CriarAutenticadoAsync(CancellationToken.None);

        cliente.IsConnected.Should().BeTrue("deve estar conectado ao servidor IMAP");
        cliente.IsAuthenticated.Should().BeTrue("deve ter autenticado com as credenciais fornecidas");

        await cliente.DisconnectAsync(quit: true);
    }

    [Fact]
    [Trait("Category", "Integracao")]
    public async Task ListarNaoLidos_DeveRetornarSemExcecao()
    {
        var config = ObterCredenciais();
        if (config is null)
            return;

        var opcoes = Options.Create(config);
        var provedor = new ProvedorTokenOAuth(opcoes, NullLogger<ProvedorTokenOAuth>.Instance);
        var opcoesConfig = Options.Create(new ConfiguracoesFiltro());
        var leitor = new LeitorEmailImap(
            new FabricaClienteImap(opcoes, provedor),
            opcoesConfig,
            NullLogger<LeitorEmailImap>.Instance);

        var emails = await leitor.ObterNaoLidosAsync(maximo: 5, CancellationToken.None);

        emails.Should().NotBeNull();
        // Não falha se a caixa estiver vazia — só verifica que não lançou exceção
    }
}
