using System.Text.Json;
using EmailSpamFilter.Application.DTOs;
using EmailSpamFilter.Application.Interfaces;
using EmailSpamFilter.Infrastructure.Configuracao;
using Microsoft.Extensions.Options;

namespace EmailSpamFilter.Infrastructure.Persistencia;

public sealed class EscritorSaudeJson : IEscritorSaude
{
    private static readonly JsonSerializerOptions OpcoesJson = new()
    {
        WriteIndented = true
    };

    private readonly string _caminhoArquivo;

    public EscritorSaudeJson(IOptions<ConfiguracoesFiltro> options)
    {
        _caminhoArquivo = Path.Combine(AppContext.BaseDirectory, options.Value.ArquivoSaude);
    }

    public async Task RegistrarAsync(InstantaneoSaude instantaneo, CancellationToken ct)
    {
        var temporario = _caminhoArquivo + ".tmp";
        var json = JsonSerializer.Serialize(instantaneo, OpcoesJson);

        await File.WriteAllTextAsync(temporario, json, ct);
        File.Move(temporario, _caminhoArquivo, overwrite: true);
    }
}