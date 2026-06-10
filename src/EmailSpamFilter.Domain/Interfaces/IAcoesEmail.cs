using EmailSpamFilter.Domain.Entidades;

namespace EmailSpamFilter.Domain.Interfaces;

public interface IAcoesEmail
{
    Task DeletarLoteAsync(IReadOnlyList<MensagemEmail> mensagens, CancellationToken ct);
    Task MoverParaLixoLoteAsync(IReadOnlyList<MensagemEmail> mensagens, CancellationToken ct);
}
