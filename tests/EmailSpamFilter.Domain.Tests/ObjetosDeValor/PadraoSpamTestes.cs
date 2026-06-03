using EmailSpamFilter.Domain.Enums;
using EmailSpamFilter.Domain.Excecoes;
using EmailSpamFilter.Domain.ObjetosDeValor;
using FluentAssertions;

namespace EmailSpamFilter.Domain.Tests.ObjetosDeValor;

public sealed class PadraoSpamTestes
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_DeveLancarExcecao_QuandoPadraoVazio(string? valor)
    {
        var acao = () => PadraoSpam.Criar(valor!);
        acao.Should().Throw<PadraoSpamInvalidoException>();
    }

    [Fact]
    public void Criar_DeveNormalizarParaMinusculo()
    {
        var padrao = PadraoSpam.Criar("CAMPANHASBRADESCO");
        padrao.Valor.Should().Be("campanhasbradesco");
    }

    [Fact]
    public void Criar_DeveUsarEscopoGlobalPorPadrao()
    {
        var padrao = PadraoSpam.Criar("teste");
        padrao.Escopo.Should().Be(EscopoCorrespondencia.Global);
    }

    [Fact]
    public void Equals_DeveSerIgual_QuandoMesmoValorEEscopo()
    {
        var a = PadraoSpam.Criar("teste", EscopoCorrespondencia.ApenasRemetente);
        var b = PadraoSpam.Criar("teste", EscopoCorrespondencia.ApenasRemetente);
        a.Should().Be(b);
    }

    [Fact]
    public void Equals_DeveSerDiferente_QuandoEscoposDiferentes()
    {
        var a = PadraoSpam.Criar("teste", EscopoCorrespondencia.Global);
        var b = PadraoSpam.Criar("teste", EscopoCorrespondencia.ApenasRemetente);
        a.Should().NotBe(b);
    }
}
