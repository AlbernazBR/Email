namespace EmailSpamFilter.Application.DTOs;

public sealed class ResultadoProcessamento
{
    public int Processados { get; init; }
    public int QuantidadeSpam { get; init; }
    public IReadOnlyList<string> Erros { get; init; } = [];
}
