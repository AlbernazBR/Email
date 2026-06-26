using System.Text.Json;
using EmailSpamFilter.Application.DTOs;

namespace EmailSpamFilter.Tray.Monitoramento;

public sealed class LeitorSaudeJson
{
    private readonly string _caminhoArquivo;

    public LeitorSaudeJson(string caminhoArquivo)
    {
        _caminhoArquivo = caminhoArquivo;
    }

    public InstantaneoSaude? Ler()
    {
        if (!File.Exists(_caminhoArquivo))
            return null;

        try
        {
            var json = File.ReadAllText(_caminhoArquivo);
            return JsonSerializer.Deserialize<InstantaneoSaude>(json);
        }
        catch (IOException)
        {
            // O arquivo pode estar em escrita pelo serviço neste instante.
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
