using EmailSpamFilter.Domain.Enums;

namespace EmailSpamFilter.Infrastructure.Configuracao;

public sealed class ConfiguracoesFiltro
{
    public int IntervaloSegundos { get; set; } = 60;
    public string ArquivoBloqueio { get; set; } = "bloqueio.json";
    public AcaoFiltro Acao { get; set; } = AcaoFiltro.Deletar;
    public int MaximoEmailsPorCiclo { get; set; } = 100;
    public bool ModoSimulacao { get; set; } = false;
}
