using EmailSpamFilter.Application.DTOs;
using EmailSpamFilter.Application.Interfaces;
using EmailSpamFilter.Infrastructure.Configuracao;
using Microsoft.Extensions.Options;

namespace EmailSpamFilter.Worker;

public sealed class TrabalhadorFiltro : BackgroundService
{
    private readonly IProcessarCaixaEntradaCasoDeUso _casoDeUso;
    private readonly ConfiguracoesFiltro _config;
    private readonly ILogger<TrabalhadorFiltro> _logger;

    public TrabalhadorFiltro(
        IProcessarCaixaEntradaCasoDeUso casoDeUso,
        IOptions<ConfiguracoesFiltro> options,
        ILogger<TrabalhadorFiltro> logger)
    {
        _casoDeUso = casoDeUso;
        _config = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TrabalhadorFiltro iniciado. Intervalo: {Intervalo}s.", _config.IntervaloSegundos);

        if (_config.ModoSimulacao)
            _logger.LogWarning(">>> MODO SIMULAÇÃO ATIVO — nenhum email será deletado. Apenas logs. <<<");

        while (!stoppingToken.IsCancellationRequested)
        {
            await ExecutarCicloAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(_config.IntervaloSegundos), stoppingToken)
                      .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        _logger.LogInformation("TrabalhadorFiltro encerrado.");
    }

    private async Task ExecutarCicloAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Iniciando varredura da caixa de entrada...");

            var opcoes = new OpcoesProcessamento
            {
                Acao = _config.Acao,
                MaximoEmailsPorCiclo = _config.MaximoEmailsPorCiclo,
                ModoSimulacao = _config.ModoSimulacao
            };

            var resultado = await _casoDeUso.ExecutarAsync(opcoes, ct);

            if (_config.ModoSimulacao)
                _logger.LogInformation(
                    "[SIMULAÇÃO] Ciclo concluído — Analisados: {Processados} | Seriam removidos: {Spam} | Erros: {Erros}",
                    resultado.Processados, resultado.QuantidadeSpam, resultado.Erros.Count);
            else
                _logger.LogInformation(
                    "Ciclo concluído — Analisados: {Processados} | Spam removido: {Spam} | Erros: {Erros}",
                    resultado.Processados, resultado.QuantidadeSpam, resultado.Erros.Count);

            foreach (var erro in resultado.Erros)
                _logger.LogWarning("Erro durante processamento: {Erro}", erro);
        }
        catch (OperationCanceledException)
        {
            // Cancelamento solicitado — encerramento normal
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no ciclo de varredura.");
        }
    }
}
