using System.ServiceProcess;
using EmailSpamFilter.Application.DTOs;

namespace EmailSpamFilter.Tray.Monitoramento;

public sealed class AvaliadorEstado
{
    private readonly int _toleranciaSegundos;
    private readonly int _limiteFalhas;

    public AvaliadorEstado(int toleranciaSegundos = 90, int limiteFalhas = 3)
    {
        _toleranciaSegundos = toleranciaSegundos;
        _limiteFalhas = limiteFalhas;
    }

    public EstadoMonitoramento Avaliar(ServiceControllerStatus? statusServico, InstantaneoSaude? saude)
    {
        if (statusServico != ServiceControllerStatus.Running)
            return EstadoMonitoramento.Parado;

        if (saude is null || HeartbeatExpirado(saude))
            return EstadoMonitoramento.Atencao;

        if (!saude.Sucesso || saude.FalhasConsecutivas >= _limiteFalhas)
            return EstadoMonitoramento.Critico;

        return EstadoMonitoramento.Saudavel;
    }

    private bool HeartbeatExpirado(InstantaneoSaude saude)
    {
        var intervalo = DateTimeOffset.UtcNow - saude.DataHoraUltimoCicloUtc;
        return intervalo > TimeSpan.FromSeconds(_toleranciaSegundos);
    }
}
