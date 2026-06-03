using EmailSpamFilter.Infrastructure.Configuracao;
using EmailSpamFilter.Infrastructure.Persistencia;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EmailSpamFilter.Infrastructure.Tests.Persistencia;

public sealed class RepositorioBloqueioJsonTestes : IDisposable
{
    private readonly string _arquivoTemp;

    public RepositorioBloqueioJsonTestes()
        => _arquivoTemp = Path.GetTempFileName();

    public void Dispose()
        => File.Delete(_arquivoTemp);

    private RepositorioBloqueioJson CriarRepositorio(string caminho)
    {
        var options = Options.Create(new ConfiguracoesFiltro { ArquivoBloqueio = caminho });
        return new RepositorioBloqueioJson(options, NullLogger<RepositorioBloqueioJson>.Instance);
    }

    [Fact]
    public async Task CarregarAsync_DeveCarregar_TodosOsPadroes()
    {
        var json = """
            {
              "padroes": ["campanhasbradesco", "bulk"],
              "padroesPorRemetente": ["mailing."],
              "padroesPorCabecalho": ["list-unsubscribe"]
            }
            """;
        await File.WriteAllTextAsync(_arquivoTemp, json);

        var repo = CriarRepositorio(_arquivoTemp);
        var regras = await repo.CarregarAsync(CancellationToken.None);

        regras.ObterRegrasAtivas().Should().HaveCount(4);
    }

    [Fact]
    public async Task CarregarAsync_DeveRetornarVazio_QuandoArquivoNaoExiste()
    {
        var repo = CriarRepositorio("arquivo_inexistente.json");

        var regras = await repo.CarregarAsync(CancellationToken.None);

        regras.ObterRegrasAtivas().Should().BeEmpty();
    }

    [Fact]
    public async Task CarregarAsync_DeveRecarregar_QuandoArquivoMuda()
    {
        await File.WriteAllTextAsync(_arquivoTemp, """{"padroes":["padrao1"]}""");
        var repo = CriarRepositorio(_arquivoTemp);

        var antes = await repo.CarregarAsync(CancellationToken.None);
        antes.ObterRegrasAtivas().Should().HaveCount(1);

        // Aguarda 1 ms para garantir que o timestamp de escrita mude
        await Task.Delay(10);
        await File.WriteAllTextAsync(_arquivoTemp, """{"padroes":["padrao1","padrao2"]}""");

        var depois = await repo.CarregarAsync(CancellationToken.None);
        depois.ObterRegrasAtivas().Should().HaveCount(2);
    }

    [Fact]
    public async Task CarregarAsync_DeveIgnorar_PadroesDuplicadosComMesmoEscopo()
    {
        var json = """{"padroes":["duplicado","duplicado"]}""";
        await File.WriteAllTextAsync(_arquivoTemp, json);

        var repo = CriarRepositorio(_arquivoTemp);
        var regras = await repo.CarregarAsync(CancellationToken.None);

        // Segundo duplicado é ignorado silenciosamente pelo repositório
        regras.ObterRegrasAtivas().Should().HaveCount(1);
    }
}
