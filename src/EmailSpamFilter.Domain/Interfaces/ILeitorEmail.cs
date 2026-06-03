using EmailSpamFilter.Domain.Entidades;

namespace EmailSpamFilter.Domain.Interfaces;

public interface ILeitorEmail
{
    Task<IReadOnlyList<MensagemEmail>> ObterNaoLidosAsync(int maximo, CancellationToken ct);
}
