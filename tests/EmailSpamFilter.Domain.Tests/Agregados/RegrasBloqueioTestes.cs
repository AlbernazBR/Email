using EmailSpamFilter.Domain.Agregados;
using EmailSpamFilter.Domain.Entidades;
using EmailSpamFilter.Domain.Enums;
using EmailSpamFilter.Domain.Excecoes;
using EmailSpamFilter.Domain.ObjetosDeValor;
using FluentAssertions;

namespace EmailSpamFilter.Domain.Tests.Agregados;

public sealed class RegrasBloqueioTestes
{
    private static RegraSpam CriarRegra(string padrao, EscopoCorrespondencia escopo = EscopoCorrespondencia.Global)
    {
        var p = PadraoSpam.Criar(padrao, escopo);
        return RegraSpam.Criar(p, escopo);
    }

    [Fact]
    public void AdicionarRegra_DeveAdicionarRegra_QuandoValida()
    {
        var regras = new RegrasBloqueio();
        regras.AdicionarRegra(CriarRegra("campanhasbradesco"));

        regras.ObterRegrasAtivas().Should().HaveCount(1);
    }

    [Fact]
    public void AdicionarRegra_DeveLancarExcecao_QuandoRegraDuplicada()
    {
        var regras = new RegrasBloqueio();
        regras.AdicionarRegra(CriarRegra("campanhasbradesco"));

        var acao = () => regras.AdicionarRegra(CriarRegra("campanhasbradesco"));

        acao.Should().Throw<ExcecaoDominioException>();
    }

    [Fact]
    public void RemoverRegra_DeveDesativarRegra()
    {
        var regras = new RegrasBloqueio();
        var regra = CriarRegra("campanhasbradesco");
        regras.AdicionarRegra(regra);

        regras.RemoverRegra(regra.Id);

        regras.ObterRegrasAtivas().Should().BeEmpty();
    }

    [Fact]
    public void RemoverRegra_DeveLancarExcecao_QuandoIdInexistente()
    {
        var regras = new RegrasBloqueio();

        var acao = () => regras.RemoverRegra(Guid.NewGuid());

        acao.Should().Throw<ExcecaoDominioException>();
    }

    [Fact]
    public void AdicionarRegra_DevePermitirMesmoPadraoComEscoposDiferentes()
    {
        var regras = new RegrasBloqueio();
        regras.AdicionarRegra(CriarRegra("campanhasbradesco", EscopoCorrespondencia.Global));
        regras.AdicionarRegra(CriarRegra("campanhasbradesco", EscopoCorrespondencia.ApenasRemetente));

        regras.ObterRegrasAtivas().Should().HaveCount(2);
    }
}
