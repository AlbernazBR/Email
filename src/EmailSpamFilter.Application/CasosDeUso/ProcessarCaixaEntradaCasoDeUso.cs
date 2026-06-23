using EmailSpamFilter.Application.DTOs;
using EmailSpamFilter.Application.Interfaces;
using EmailSpamFilter.Domain.Entidades;
using EmailSpamFilter.Domain.Enums;
using EmailSpamFilter.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace EmailSpamFilter.Application.CasosDeUso;

public sealed class ProcessarCaixaEntradaCasoDeUso : IProcessarCaixaEntradaCasoDeUso
{
    private readonly ILeitorEmail _leitor;
    private readonly IAcoesEmail _acoes;
    private readonly IRepositorioBloqueio _repositorio;
    private readonly ILogger<ProcessarCaixaEntradaCasoDeUso> _logger;

    public ProcessarCaixaEntradaCasoDeUso(
        ILeitorEmail leitor,
        IAcoesEmail acoes,
        IRepositorioBloqueio repositorio,
        ILogger<ProcessarCaixaEntradaCasoDeUso> logger)
    {
        _leitor = leitor;
        _acoes = acoes;
        _repositorio = repositorio;
        _logger = logger;
    }

    public async Task<ResultadoProcessamento> ExecutarAsync(OpcoesProcessamento opcoes, CancellationToken ct)
    {
        var regrasBloqueio = await _repositorio.CarregarAsync(ct);
        var regrasAtivas = regrasBloqueio.ObterRegrasAtivas();
        var impostoras = regrasBloqueio.ObterImpostoras();
        var remetenteRegex = regrasBloqueio.ObterRemetenteRegex();

        var mensagens = await _leitor.ObterNaoLidosAsync(opcoes.MaximoEmailsPorCiclo, ct);

        var spamParaDeletar = new List<MensagemEmail>();
        var spamParaMover = new List<MensagemEmail>();
        var erros = new List<string>();

        foreach (var mensagem in mensagens)
        {
            try
            {
                ClassificarMensagem(mensagem, regrasAtivas, impostoras, remetenteRegex, opcoes, spamParaDeletar, spamParaMover);
            }
            catch (Exception ex)
            {
                erros.Add($"UID {mensagem.UidImap}: {ex.Message}");
            }
        }

        if (!opcoes.ModoSimulacao)
            await ExecutarAcoesLoteAsync(spamParaDeletar, spamParaMover, erros, ct);

        return new ResultadoProcessamento
        {
            Processados = mensagens.Count,
            QuantidadeSpam = spamParaDeletar.Count + spamParaMover.Count,
            Erros = erros.AsReadOnly()
        };
    }

    private void ClassificarMensagem(
        MensagemEmail mensagem,
        IReadOnlyList<RegraSpam> regrasAtivas,
        IReadOnlyList<RegraImpostora> impostoras,
        IReadOnlyList<RegraRemetenteRegex> remetenteRegex,
        OpcoesProcessamento opcoes,
        List<MensagemEmail> spamParaDeletar,
        List<MensagemEmail> spamParaMover)
    {
        var resultado = mensagem.Analisar(regrasAtivas, impostoras, remetenteRegex);

        if (resultado.EhSpam)
        {
            _logger.LogInformation(
                "[SPAM]{Modo} De: {NomeExibido} <{Remetente}> | Assunto: {Assunto} | Regra: {Regra} | Campo: {Campo}",
                opcoes.ModoSimulacao ? " [SIM]" : "",
                mensagem.Remetente.NomeExibido,
                mensagem.Remetente.Valor,
                mensagem.Assunto,
                resultado.RegraCorrespondente,
                resultado.CampoCorrespondente);

            if (opcoes.ModoSimulacao)
            {
                spamParaDeletar.Add(mensagem);
                return;
            }

            if (opcoes.Acao == AcaoFiltro.MoverParaLixo)
                spamParaMover.Add(mensagem);
            else
                spamParaDeletar.Add(mensagem);
        }
        else
        {
            _logger.LogDebug(
                "[LIMPO] De: {NomeExibido} <{Remetente}> | Assunto: {Assunto}",
                mensagem.Remetente.NomeExibido,
                mensagem.Remetente.Valor,
                mensagem.Assunto);
        }
    }

    private async Task ExecutarAcoesLoteAsync(
        List<MensagemEmail> spamParaDeletar,
        List<MensagemEmail> spamParaMover,
        List<string> erros,
        CancellationToken ct)
    {
        try
        {
            if (spamParaDeletar.Count > 0)
                await _acoes.DeletarLoteAsync(spamParaDeletar, ct);

            if (spamParaMover.Count > 0)
                await _acoes.MoverParaLixoLoteAsync(spamParaMover, ct);
        }
        catch (Exception ex)
        {
            erros.Add($"Erro na operação em lote: {ex.Message}");
        }
    }
}
