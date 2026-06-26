namespace EmailSpamFilter.Application.DTOs;

public sealed class InstantaneoSaude
{
    public string Servico { get; init; } = "EmailSpamFilter";
    public DateTimeOffset DataHoraUltimoCicloUtc { get; init; }
    public int Processados { get; init; }
    public int QuantidadeSpam { get; init; }
    public int QuantidadeErros { get; init; }
    public bool Sucesso { get; init; }
    public bool ModoSimulacao { get; init; }
    public int FalhasConsecutivas { get; init; }
}