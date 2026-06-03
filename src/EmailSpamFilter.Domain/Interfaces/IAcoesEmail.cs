using EmailSpamFilter.Domain.Entidades;

namespace EmailSpamFilter.Domain.Interfaces;

public interface IAcoesEmail
{
    Task DeletarAsync(MensagemEmail mensagem, CancellationToken ct);
    Task MoverParaLixoAsync(MensagemEmail mensagem, CancellationToken ct);

    /// <summary>
    /// Deleta todos os emails da lista em uma única sessão IMAP (evita throttle).
    /// </summary>
    Task DeletarLoteAsync(IReadOnlyList<MensagemEmail> mensagens, CancellationToken ct);

    /// <summary>
    /// Move todos os emails da lista para o Lixo em uma única sessão IMAP.
    /// </summary>
    Task MoverParaLixoLoteAsync(IReadOnlyList<MensagemEmail> mensagens, CancellationToken ct);
}
