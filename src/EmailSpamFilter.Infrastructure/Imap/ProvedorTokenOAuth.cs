using EmailSpamFilter.Infrastructure.Configuracao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace EmailSpamFilter.Infrastructure.Imap;

/// <summary>
/// Obtém token OAuth2 via Device Code Flow (MSAL).
/// Na primeira execução exibe URL e código no log — o usuário autentica no browser.
/// Nas execuções seguintes usa o refresh token cacheado em disco automaticamente.
/// </summary>
public sealed class ProvedorTokenOAuth
{
    private static readonly string[] Escopos =
    [
        "https://outlook.office.com/IMAP.AccessAsUser.All",
        "offline_access"
    ];

    private readonly ConfiguracoesImap _config;
    private readonly ILogger<ProvedorTokenOAuth> _logger;
    private IPublicClientApplication? _app;

    public ProvedorTokenOAuth(IOptions<ConfiguracoesImap> options, ILogger<ProvedorTokenOAuth> logger)
    {
        _config = options.Value;
        _logger = logger;
    }

    private async Task<IPublicClientApplication> ObterAppAsync()
    {
        if (_app is not null)
            return _app;

        _app = PublicClientApplicationBuilder
            .Create(_config.ClientId)
            .WithAuthority(AadAuthorityAudience.PersonalMicrosoftAccount)
            .WithRedirectUri("http://localhost")
            .Build();

        // Cache persistente em disco — o refresh token sobrevive a reinicializações do serviço
        var storageProperties = new StorageCreationPropertiesBuilder(
                "emailspamfilter_token_cache.bin",
                MsalCacheHelper.UserRootDirectory)
            .Build();

        var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
        cacheHelper.RegisterCache(_app.UserTokenCache);

        return _app;
    }

    public async Task<string> ObterTokenAsync(CancellationToken ct)
    {
        var app = await ObterAppAsync();

        // Tenta autenticação silenciosa com o token cacheado
        var contas = await app.GetAccountsAsync();
        var conta = contas.FirstOrDefault(c =>
            c.Username.Equals(_config.Email, StringComparison.OrdinalIgnoreCase));

        if (conta is not null)
        {
            try
            {
                var resultado = await app.AcquireTokenSilent(Escopos, conta)
                    .ExecuteAsync(ct);

                _logger.LogDebug("Token OAuth2 obtido silenciosamente para {Email}", _config.Email);
                return resultado.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                _logger.LogInformation("Token expirado — iniciando Device Code Flow");
            }
        }

        // Device Code Flow — exige interação do usuário uma única vez
        var resultadoCodigo = await app.AcquireTokenWithDeviceCode(Escopos, callback =>
        {
            _logger.LogWarning(
                "AUTENTICAÇÃO NECESSÁRIA — Abra {Url} e insira o código: {Codigo}",
                callback.VerificationUrl,
                callback.UserCode);

            // Também imprime no console para garantir visibilidade
            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║  AUTENTICAÇÃO OAuth2 NECESSÁRIA                  ║");
            Console.WriteLine($"║  1. Abra: {callback.VerificationUrl,-39}║");
            Console.WriteLine($"║  2. Código: {callback.UserCode,-38}║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.WriteLine();

            return Task.CompletedTask;
        }).ExecuteAsync(ct);

        _logger.LogInformation("Autenticado via OAuth2 para {Email}", resultadoCodigo.Account.Username);
        return resultadoCodigo.AccessToken;
    }
}
