using EmailSpamFilter.Domain.Agregados;

namespace EmailSpamFilter.Domain.Interfaces;

public interface IRepositorioBloqueio
{
    Task<RegrasBloqueio> CarregarAsync(CancellationToken ct);
}
