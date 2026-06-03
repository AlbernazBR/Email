using EmailSpamFilter.Domain.Entidades;
using EmailSpamFilter.Domain.Interfaces;
using MailKit;
using MailKit.Net.Imap;

namespace EmailSpamFilter.Infrastructure.Imap;

public sealed class AcoesEmailImap : IAcoesEmail
{
    private readonly FabricaClienteImap _fabrica;

    public AcoesEmailImap(FabricaClienteImap fabrica) => _fabrica = fabrica;

    public Task DeletarAsync(MensagemEmail mensagem, CancellationToken ct)
        => DeletarLoteAsync([mensagem], ct);

    public Task MoverParaLixoAsync(MensagemEmail mensagem, CancellationToken ct)
        => MoverParaLixoLoteAsync([mensagem], ct);

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
                if (lixo is not null)
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

    private static IMailFolder? ResolverPastaLixo(ImapClient client)
    {
        try { return client.GetFolder(SpecialFolder.Junk); }
        catch { /* pasta Junk não disponível */ }

        try { return client.GetFolder(SpecialFolder.Trash); }
        catch { /* pasta Trash não disponível */ }

        return null;
    }
}
