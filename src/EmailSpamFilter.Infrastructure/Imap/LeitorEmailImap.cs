using EmailSpamFilter.Domain.Entidades;
using EmailSpamFilter.Domain.Interfaces;
using EmailSpamFilter.Infrastructure.Configuracao;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmailSpamFilter.Infrastructure.Imap;

public sealed class LeitorEmailImap : ILeitorEmail
{
    private readonly FabricaClienteImap _fabrica;
    private readonly ILogger<LeitorEmailImap> _logger;

    private static readonly string[] _nomesLixo =
        ["Junk Email", "Lixo Eletrônico", "Junk", "Spam", "Lixo"];

    public LeitorEmailImap(
        FabricaClienteImap fabrica,
        IOptions<ConfiguracoesFiltro> options,
        ILogger<LeitorEmailImap> logger)
    {
        _fabrica = fabrica;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MensagemEmail>> ObterNaoLidosAsync(int maximo, CancellationToken ct)
    {
        using var client = await _fabrica.CriarAutenticadoAsync(ct);
        var resultado = new List<MensagemEmail>();

        var pastaLixo = ObterPastaLixo(client);
        if (pastaLixo is not null)
            await EscanearPastaAsync(client, pastaLixo, maximo, resultado, ct);
        else
            _logger.LogWarning("Pasta Lixo Eletrônico não encontrada no servidor IMAP.");

        await client.DisconnectAsync(true, ct);
        return resultado.AsReadOnly();
    }

    private IMailFolder? ObterPastaLixo(ImapClient client)
    {
        // Tenta pelo atributo especial \Junk
        try
        {
            var pasta = client.GetFolder(SpecialFolder.Junk);
            _logger.LogDebug("Pasta Lixo encontrada via SpecialFolder: {Nome}", pasta.FullName);
            return pasta;
        }
        catch { }

        // Fallback: busca por nome nas pastas pessoais (Outlook pode não expor \Junk)
        try
        {
            var raiz = client.GetFolder(client.PersonalNamespaces[0]);
            foreach (var subpasta in raiz.GetSubfolders(false))
            {
                if (_nomesLixo.Any(n => string.Equals(subpasta.Name, n, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogDebug("Pasta Lixo encontrada por nome: {Nome}", subpasta.FullName);
                    return subpasta;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Erro ao enumerar pastas para localizar Lixo Eletrônico: {Erro}", ex.Message);
        }

        return null;
    }

    private async Task EscanearPastaAsync(
        ImapClient client,
        IMailFolder pasta,
        int maximo,
        List<MensagemEmail> resultado,
        CancellationToken ct)
    {
        await pasta.OpenAsync(FolderAccess.ReadOnly, ct);

        var uids = await pasta.SearchAsync(SearchQuery.All, ct);
        var limite = maximo > 0 ? Math.Min(maximo, uids.Count) : uids.Count;

        _logger.LogInformation("Escaneando pasta '{Pasta}': {Total} email(s), lendo {Limite}.",
            pasta.FullName, uids.Count, limite);

        foreach (var uid in uids.Take(limite))
        {
            var mime = await pasta.GetMessageAsync(uid, ct);
            resultado.Add(MapeadorMensagemEmail.Mapear(mime, uid.ToString(), pasta.FullName));
        }

        await pasta.CloseAsync(expunge: false, ct);
    }
}
