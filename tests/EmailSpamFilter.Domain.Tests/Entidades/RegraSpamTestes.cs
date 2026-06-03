using EmailSpamFilter.Domain.Entidades;
using EmailSpamFilter.Domain.Enums;
using EmailSpamFilter.Domain.ObjetosDeValor;
using FluentAssertions;

namespace EmailSpamFilter.Domain.Tests.Entidades;

public sealed class RegraSpamTestes
{
    private static MensagemEmail CriarMensagem(
        string remetente = "usuario@normal.com",
        string assunto = "assunto normal",
        string corpo = "corpo normal",
        IReadOnlyList<CabecalhoEmail>? cabecalhos = null)
    {
        return new MensagemEmail(
            uidImap: "1",
            remetente: EnderecoEmail.Criar(remetente),
            assunto: assunto,
            cabecalhos: cabecalhos ?? [],
            corpo: corpo);
    }

    [Fact]
    public void Corresponde_DeveRetornarVerdadeiro_QuandoPadraoNoRemetente()
    {
        var padrao = PadraoSpam.Criar("campanhasbradesco", EscopoCorrespondencia.ApenasRemetente);
        var regra = RegraSpam.Criar(padrao, EscopoCorrespondencia.ApenasRemetente);
        var mensagem = CriarMensagem(remetente: "banco@campanhasbradesco500.com");

        regra.Corresponde(mensagem).Should().BeTrue();
    }

    [Fact]
    public void Corresponde_DeveRetornarFalso_QuandoEscopoApenasRemetenteEPadraoNoAssunto()
    {
        var padrao = PadraoSpam.Criar("campanhasbradesco", EscopoCorrespondencia.ApenasRemetente);
        var regra = RegraSpam.Criar(padrao, EscopoCorrespondencia.ApenasRemetente);
        var mensagem = CriarMensagem(remetente: "legitimo@banco.com", assunto: "campanhasbradesco");

        regra.Corresponde(mensagem).Should().BeFalse();
    }

    [Fact]
    public void Corresponde_DeveRetornarVerdadeiro_QuandoPadraoNoCabecalho()
    {
        var cabecalhos = new List<CabecalhoEmail>
        {
            new("List-Unsubscribe", "<mailto:unsubscribe@campanhasbradesco.com>")
        };
        var padrao = PadraoSpam.Criar("campanhasbradesco", EscopoCorrespondencia.ApenasCabecalho);
        var regra = RegraSpam.Criar(padrao, EscopoCorrespondencia.ApenasCabecalho);
        var mensagem = CriarMensagem(cabecalhos: cabecalhos);

        regra.Corresponde(mensagem).Should().BeTrue();
    }

    [Fact]
    public void Corresponde_DeveRetornarFalso_QuandoRegraInativa()
    {
        var padrao = PadraoSpam.Criar("campanhasbradesco");
        var regra = RegraSpam.Criar(padrao, EscopoCorrespondencia.Global);
        regra.Desativar();

        var mensagem = CriarMensagem(remetente: "banco@campanhasbradesco.com");

        regra.Corresponde(mensagem).Should().BeFalse();
    }

    [Fact]
    public void Corresponde_Global_DeveVerificarTodosOsCampos()
    {
        var padrao = PadraoSpam.Criar("unsubscribe", EscopoCorrespondencia.Global);
        var regra = RegraSpam.Criar(padrao, EscopoCorrespondencia.Global);
        var mensagem = CriarMensagem(corpo: "click here to unsubscribe from our list");

        regra.Corresponde(mensagem).Should().BeTrue();
    }
}
