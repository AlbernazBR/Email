using EmailSpamFilter.Domain.Entidades;
using EmailSpamFilter.Domain.Enums;
using EmailSpamFilter.Domain.ObjetosDeValor;
using FluentAssertions;

namespace EmailSpamFilter.Domain.Tests.Entidades;

public sealed class MensagemEmailTestes
{
    private static RegraSpam CriarRegra(string padrao, EscopoCorrespondencia escopo = EscopoCorrespondencia.Global)
    {
        var p = PadraoSpam.Criar(padrao, escopo);
        return RegraSpam.Criar(p, escopo);
    }

    private static MensagemEmail CriarMensagem(string remetente = "user@normal.com", string assunto = "normal")
    {
        return new MensagemEmail("1", EnderecoEmail.Criar(remetente), assunto, [], "corpo");
    }

    [Fact]
    public void Analisar_DeveRetornarSpam_QuandoRegraCorresponde()
    {
        var regras = new[] { CriarRegra("campanhasbradesco") };
        var mensagem = CriarMensagem(remetente: "banco@campanhasbradesco.com");

        var resultado = mensagem.Analisar(regras);

        resultado.EhSpam.Should().BeTrue();
        resultado.RegraCorrespondente.Should().NotBeNull();
    }

    [Fact]
    public void Analisar_DeveRetornarLimpo_QuandoNenhumaRegraCorresponde()
    {
        var regras = new[] { CriarRegra("campanhasbradesco") };
        var mensagem = CriarMensagem(remetente: "contato@empresa.com");

        var resultado = mensagem.Analisar(regras);

        resultado.EhSpam.Should().BeFalse();
        resultado.RegraCorrespondente.Should().BeNull();
    }

    [Fact]
    public void Analisar_DeveRetornarLimpo_QuandoListaDeRegrasVazia()
    {
        var mensagem = CriarMensagem(remetente: "banco@campanhasbradesco.com");

        var resultado = mensagem.Analisar([]);

        resultado.EhSpam.Should().BeFalse();
    }

    [Fact]
    public void Analisar_DeveUsarPrimeiraRegraCorrespondente()
    {
        var regra1 = CriarRegra("campanhasbradesco");
        var regra2 = CriarRegra("bulk");
        var mensagem = CriarMensagem(remetente: "banco@campanhasbradesco.com", assunto: "bulk offer");

        var resultado = mensagem.Analisar([regra1, regra2]);

        resultado.EhSpam.Should().BeTrue();
        resultado.RegraCorrespondente.Should().Contain("campanhasbradesco");
    }
}
