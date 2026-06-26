using EmailSpamFilter.Application.CasosDeUso;
using EmailSpamFilter.Application.DTOs;
using EmailSpamFilter.Domain.Agregados;
using EmailSpamFilter.Domain.Entidades;
using EmailSpamFilter.Domain.Enums;
using EmailSpamFilter.Domain.Interfaces;
using EmailSpamFilter.Domain.ObjetosDeValor;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EmailSpamFilter.Application.Tests.CasosDeUso;

public sealed class ProcessarCaixaEntradaTestes
{
    private readonly ILeitorEmail _leitor = Substitute.For<ILeitorEmail>();
    private readonly IAcoesEmail _acoes = Substitute.For<IAcoesEmail>();
    private readonly IRepositorioBloqueio _repositorio = Substitute.For<IRepositorioBloqueio>();

    private ProcessarCaixaEntradaCasoDeUso CriarCasoDeUso()
        => new(_leitor, _acoes, _repositorio, NullLogger<ProcessarCaixaEntradaCasoDeUso>.Instance);

    private static MensagemEmail CriarMensagem(string remetente, string uid = "1")
        => new(uid, EnderecoEmail.Criar(remetente), "assunto", [], "corpo");

    private static RegrasBloqueio CriarRegras(params string[] padroes)
    {
        var regras = new RegrasBloqueio();
        foreach (var p in padroes)
            regras.AdicionarRegra(RegraSpam.Criar(
                PadraoSpam.Criar(p, EscopoCorrespondencia.Global),
                EscopoCorrespondencia.Global));
        return regras;
    }

    [Fact]
    public async Task ExecutarAsync_DeveDeletar_QuandoEmailEhSpam()
    {
        var mensagem = CriarMensagem("banco@campanhasbradesco.com");
        _leitor.ObterNaoLidosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
               .Returns([mensagem]);
        _repositorio.CarregarAsync(Arg.Any<CancellationToken>())
                    .Returns(CriarRegras("campanhasbradesco"));

        var resultado = await CriarCasoDeUso().ExecutarAsync(
            new OpcoesProcessamento { Acao = AcaoFiltro.Deletar }, CancellationToken.None);

        await _acoes.Received(1).DeletarLoteAsync(
            Arg.Is<IReadOnlyList<MensagemEmail>>(l => l.Count == 1 && l[0] == mensagem),
            Arg.Any<CancellationToken>());
        resultado.QuantidadeSpam.Should().Be(1);
    }

    [Fact]
    public async Task ExecutarAsync_NaoDeveChamarAcoes_QuandoEmailEhLimpo()
    {
        var mensagem = CriarMensagem("contato@empresa.com");
        _leitor.ObterNaoLidosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
               .Returns([mensagem]);
        _repositorio.CarregarAsync(Arg.Any<CancellationToken>())
                    .Returns(CriarRegras("campanhasbradesco"));

        var resultado = await CriarCasoDeUso().ExecutarAsync(
            new OpcoesProcessamento(), CancellationToken.None);

        await _acoes.DidNotReceive().DeletarLoteAsync(
            Arg.Any<IReadOnlyList<MensagemEmail>>(), Arg.Any<CancellationToken>());
        await _acoes.DidNotReceive().MoverParaLixoLoteAsync(
            Arg.Any<IReadOnlyList<MensagemEmail>>(), Arg.Any<CancellationToken>());
        resultado.QuantidadeSpam.Should().Be(0);
    }

    [Fact]
    public async Task ExecutarAsync_DeveMover_QuandoAcaoEhMoverParaLixo()
    {
        var mensagem = CriarMensagem("banco@campanhasbradesco.com");
        _leitor.ObterNaoLidosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
               .Returns([mensagem]);
        _repositorio.CarregarAsync(Arg.Any<CancellationToken>())
                    .Returns(CriarRegras("campanhasbradesco"));

        await CriarCasoDeUso().ExecutarAsync(
            new OpcoesProcessamento { Acao = AcaoFiltro.MoverParaLixo }, CancellationToken.None);

        await _acoes.Received(1).MoverParaLixoLoteAsync(
            Arg.Is<IReadOnlyList<MensagemEmail>>(l => l.Count == 1 && l[0] == mensagem),
            Arg.Any<CancellationToken>());
        await _acoes.DidNotReceive().DeletarLoteAsync(
            Arg.Any<IReadOnlyList<MensagemEmail>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecutarAsync_DeveRetornarContagensCorretas()
    {
        var spam1 = CriarMensagem("banco@campanhasbradesco.com", "1");
        var spam2 = CriarMensagem("news@bulk-sender.com", "2");
        var limpo = CriarMensagem("contato@empresa.com", "3");

        _leitor.ObterNaoLidosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
               .Returns([spam1, spam2, limpo]);
        _repositorio.CarregarAsync(Arg.Any<CancellationToken>())
                    .Returns(CriarRegras("campanhasbradesco", "bulk"));

        var resultado = await CriarCasoDeUso().ExecutarAsync(
            new OpcoesProcessamento(), CancellationToken.None);

        resultado.Processados.Should().Be(3);
        resultado.QuantidadeSpam.Should().Be(2);
        resultado.Erros.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecutarAsync_DeveContinuar_QuandoLoteDeEmailsFalha()
    {
        var mensagem1 = CriarMensagem("banco@campanhasbradesco.com", "1");
        var mensagem2 = CriarMensagem("banco@campanhasbradesco.com", "2");

        _leitor.ObterNaoLidosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
               .Returns([mensagem1, mensagem2]);
        _repositorio.CarregarAsync(Arg.Any<CancellationToken>())
                    .Returns(CriarRegras("campanhasbradesco"));

        _acoes.DeletarLoteAsync(Arg.Any<IReadOnlyList<MensagemEmail>>(), Arg.Any<CancellationToken>())
              .Returns(_ => throw new InvalidOperationException("Falha IMAP simulada"));

        var resultado = await CriarCasoDeUso().ExecutarAsync(
            new OpcoesProcessamento { Acao = AcaoFiltro.Deletar }, CancellationToken.None);

        resultado.Erros.Should().HaveCount(1);
        resultado.QuantidadeSpam.Should().Be(2);
    }

    [Fact]
    public async Task ExecutarAsync_NaoDeveClassificarComoSpam_QuandoRemetenteEstaPermitido()
    {
        var mensagem = CriarMensagem("99pay@novidades.99app.com");
        _leitor.ObterNaoLidosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
               .Returns([mensagem]);

        var regras = CriarRegras("unsubscribe");
        regras.AdicionarPermitidoRemetente("99pay@novidades.99app.com");

        _repositorio.CarregarAsync(Arg.Any<CancellationToken>())
                    .Returns(regras);

        var resultado = await CriarCasoDeUso().ExecutarAsync(
            new OpcoesProcessamento { Acao = AcaoFiltro.Deletar }, CancellationToken.None);

        await _acoes.DidNotReceive().DeletarLoteAsync(
            Arg.Any<IReadOnlyList<MensagemEmail>>(), Arg.Any<CancellationToken>());
        resultado.QuantidadeSpam.Should().Be(0);
    }
}
