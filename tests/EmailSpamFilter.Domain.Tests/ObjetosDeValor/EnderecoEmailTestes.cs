using EmailSpamFilter.Domain.Excecoes;
using EmailSpamFilter.Domain.ObjetosDeValor;
using FluentAssertions;

namespace EmailSpamFilter.Domain.Tests.ObjetosDeValor;

public sealed class EnderecoEmailTestes
{
    [Fact]
    public void Criar_DeveLancarExcecao_QuandoEmailSemArroba()
    {
        var acao = () => EnderecoEmail.Criar("emailsemarroba");
        acao.Should().Throw<EnderecoEmailInvalidoException>();
    }

    [Fact]
    public void Criar_DeveLancarExcecao_QuandoEmailNulo()
    {
        var acao = () => EnderecoEmail.Criar(null!);
        acao.Should().Throw<EnderecoEmailInvalidoException>();
    }

    [Fact]
    public void Criar_DeveLancarExcecao_QuandoEmailVazio()
    {
        var acao = () => EnderecoEmail.Criar(string.Empty);
        acao.Should().Throw<EnderecoEmailInvalidoException>();
    }

    [Fact]
    public void Criar_DeveNormalizarParaMinusculo()
    {
        var email = EnderecoEmail.Criar("USUARIO@OUTLOOK.COM");
        email.Valor.Should().Be("usuario@outlook.com");
    }

    [Fact]
    public void Criar_DeveRemoverEspacos()
    {
        var email = EnderecoEmail.Criar("  usuario@outlook.com  ");
        email.Valor.Should().Be("usuario@outlook.com");
    }

    [Fact]
    public void Equals_DeveSerIgual_QuandoMesmoValor()
    {
        var a = EnderecoEmail.Criar("usuario@outlook.com");
        var b = EnderecoEmail.Criar("usuario@outlook.com");
        a.Should().Be(b);
    }

    [Fact]
    public void Contem_DeveRetornarVerdadeiro_QuandoSubstringPresente()
    {
        var email = EnderecoEmail.Criar("banco.bradesco@campanhasbradesco.com");
        email.Contem("campanhasbradesco").Should().BeTrue();
    }

    [Fact]
    public void Contem_DeveSerCaseInsensitive()
    {
        var email = EnderecoEmail.Criar("banco.bradesco@CAMPANHASBRADESCO.COM");
        email.Contem("campanhasbradesco").Should().BeTrue();
    }
}
