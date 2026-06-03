using EmailSpamFilter.Domain.Enums;

namespace EmailSpamFilter.Application.DTOs;

public sealed class OpcoesProcessamento
{
    public AcaoFiltro Acao { get; init; } = AcaoFiltro.Deletar;
    public int MaximoEmailsPorCiclo { get; init; } = 100;
    public bool ModoSimulacao { get; init; } = false;
}
