using EmailSpamFilter.Domain.Entidades;
using EmailSpamFilter.Domain.Interfaces;
using MailKit;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;

namespace EmailSpamFilter.Infrastructure.Imap;

public sealed class AcoesEmailImap : IAcoesEmail
{
    private readonly FabricaClienteImap _fabrica;
    private readonly ILogger<AcoesEmailImap> _logger;

    public AcoesEmailImap(FabricaClienteImap fabrica, ILogger<AcoesEmailImap> logger)
    {
        _fabrica = fabrica;
        _logger = logger;
    }

    public async Task DeletarLoteAsync(IReadOnlyList<MensagemEmail> mensagens, CancellationToken ct)
    {
        if (mensagens.Count == 0) return;

        using var client = await _fabrica.CriarAutenticadoAsync(ct);

        foreach (var grupo in AgruparPorPasta(mensagens))
        {
            var pasta = await AbrirPastaAsync(client, grupo.Key, ct);
            var uids = ExtrairUids(grupo);

            if (uids.Count > 0)
            {
                await pasta.AddFlagsAsync(uids, MessageFlags.Deleted, true, ct);
                await pasta.ExpungeAsync(ct);
            }

            await pasta.CloseAsync(expunge: false, ct);
        }

        await client.DisconnectAsync(true, ct);
    }

    public async Task MoverParaLixoLoteAsync(IReadOnlyList<MensagemEmail> mensagens, CancellationToken ct)
    {
        if (mensagens.Count == 0) return;

        using var client = await _fabrica.CriarAutenticadoAsync(ct);
        var lixo = ResolverPastaLixo(client);

        if (lixo is null)
        {
            _logger.LogError(
                "Pasta de lixo não encontrada no servidor IMAP. " +
                "{Count} email(s) NÃO foram movidos para evitar exclusão acidental.",
                mensagens.Count);
            return;
        }

        _logger.LogDebug("Pasta de lixo resolvida: {Pasta}", lixo.FullName);

        foreach (var grupo in AgruparPorPasta(mensagens))
        {
            var pasta = await AbrirPastaAsync(client, grupo.Key, ct);
            var uids = ExtrairUids(grupo);

            if (uids.Count > 0 && lixo.FullName != pasta.FullName)
                await pasta.MoveToAsync(uids, lixo, ct);

            await pasta.CloseAsync(expunge: false, ct);
        }

        await client.DisconnectAsync(true, ct);
    }

    private static IEnumerable<IGrouping<string, MensagemEmail>> AgruparPorPasta(
        IReadOnlyList<MensagemEmail> mensagens)
        => mensagens.GroupBy(m => string.IsNullOrEmpty(m.PastaOrigem) ? "INBOX" : m.PastaOrigem);

    private static List<UniqueId> ExtrairUids(IEnumerable<MensagemEmail> mensagens)
        => mensagens
            .Select(m => UniqueId.TryParse(m.UidImap, out var uid) ? (UniqueId?)uid : null)
            .Where(u => u.HasValue)
            .Select(u => u!.Value)
            .ToList();

    private static async Task<IMailFolder> AbrirPastaAsync(
        ImapClient client, string nomePasta, CancellationToken ct)
    {
        IMailFolder pasta = nomePasta == "INBOX" || string.IsNullOrEmpty(nomePasta)
            ? client.Inbox
            : client.GetFolder(nomePasta);
        await pasta.OpenAsync(FolderAccess.ReadWrite, ct);
        return pasta;
    }

    private static readonly string[] _nomesLixeira =
        ["Deleted Items", "Itens Excluídos", "Trash", "Lixeira", "Deleted",
         "Junk Email", "Lixo Eletrônico", "Junk", "Spam"];

    private IMailFolder? ResolverPastaLixo(ImapClient client)
    {
        // 1. Tenta pelo atributo especial \Trash (Deleted Items no Outlook)
        try
        {
            var pasta = client.GetFolder(SpecialFolder.Trash);
            if (pasta is not null)
            {
                _logger.LogDebug("Lixeira resolvida via SpecialFolder.Trash: {Nome}", pasta.FullName);
                return pasta;
            }
        }
        catch { /* atributo não disponível */ }

        // 2. Tenta pelo atributo especial \Junk (Lixo Eletrônico no Outlook)
        try
        {
            var pasta = client.GetFolder(SpecialFolder.Junk);
            if (pasta is not null)
            {
                _logger.LogDebug("Lixeira resolvida via SpecialFolder.Junk: {Nome}", pasta.FullName);
                return pasta;
            }
        }
        catch { /* atributo não disponível */ }

        // 3. Fallback: busca por nome em todas as pastas do namespace pessoal
        try
        {
            var raiz = client.GetFolder(client.PersonalNamespaces[0]);
            var todasPastas = raiz.GetSubfolders(subscribedOnly: false);
            foreach (var subpasta in todasPastas)
            {
                if (_nomesLixeira.Any(n => string.Equals(subpasta.Name, n, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogDebug("Lixeira resolvida por nome: {Nome}", subpasta.FullName);
                    return subpasta;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao buscar pastas do namespace pessoal.");
        }

        _logger.LogWarning(
            "Nenhuma pasta de lixo encontrada. Pastas disponíveis: {Pastas}",
            string.Join(", ", TentarListarPastas(client)));

        return null;
    }

    private static IEnumerable<string> TentarListarPastas(ImapClient client)
    {
        try
        {
            var raiz = client.GetFolder(client.PersonalNamespaces[0]);
            return raiz.GetSubfolders(false).Select(p => p.FullName);
        }
        catch
        {
            return ["(erro ao listar)"];
        }
    }
}