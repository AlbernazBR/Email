using EmailSpamFilter.Domain.Entidades;
using EmailSpamFilter.Domain.Interfaces;
using MailKit;
using MailKit.Net.Imap;

namespace EmailSpamFilter.Infrastructure.Imap;

public sealed class AcoesEmailImap : IAcoesEmail
{
    private readonly FabricaClienteImap _fabrica;

    public AcoesEmailImap(FabricaClienteImap fabrica) => _fabrica = fabrica;

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

        foreach (var grupo in AgruparPorPasta(mensagens))
        {
            var pasta = await AbrirPastaAsync(client, grupo.Key, ct);
            var uids = ExtrairUids(grupo);

            if (uids.Count > 0)
            {
                if (lixo is not null && lixo.FullName != pasta.FullName)
                    await pasta.MoveToAsync(uids, lixo, ct);
                else
                    await pasta.AddFlagsAsync(uids, MessageFlags.Deleted, true, ct);
            }

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
        ["Deleted Items", "Itens Excluídos", "Trash", "Lixeira", "Deleted"];

    private static IMailFolder? ResolverPastaLixo(ImapClient client)
    {
        // Tenta pelo atributo especial \Trash (Deleted Items no Outlook)
        try
        {
            var pasta = client.GetFolder(SpecialFolder.Trash);
            return pasta;
        }
        catch { /* pasta Trash não disponível via atributo especial */ }

        // Fallback: busca por nome comum da lixeira nas pastas pessoais
        try
        {
            var raiz = client.GetFolder(client.PersonalNamespaces[0]);
            foreach (var subpasta in raiz.GetSubfolders(false))
            {
                if (_nomesLixeira.Any(n => string.Equals(subpasta.Name, n, StringComparison.OrdinalIgnoreCase)))
                    return subpasta;
            }
        }
        catch { /* fallback falhou */ }

        return null;
    }
}
