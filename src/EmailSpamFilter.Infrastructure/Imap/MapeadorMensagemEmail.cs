using EmailSpamFilter.Domain.Entidades;
using EmailSpamFilter.Domain.ObjetosDeValor;
using MimeKit;

namespace EmailSpamFilter.Infrastructure.Imap;

public static class MapeadorMensagemEmail
{
    public static MensagemEmail Mapear(MimeMessage origem, string uid, string pastaOrigem = "INBOX")
    {
        var mailbox = origem.From.Mailboxes.FirstOrDefault();
        var remetente = EnderecoEmail.Criar(
            mailbox?.Address ?? "desconhecido@desconhecido.com",
            mailbox?.Name ?? string.Empty);

        var cabecalhos = origem.Headers
            .Select(h => new CabecalhoEmail(h.Field, h.Value))
            .ToList()
            .AsReadOnly();

        return new MensagemEmail(
            uidImap: uid,
            remetente: remetente,
            assunto: origem.Subject ?? string.Empty,
            cabecalhos: cabecalhos,
            corpo: origem.TextBody ?? string.Empty,
            pastaOrigem: pastaOrigem);
    }
}
