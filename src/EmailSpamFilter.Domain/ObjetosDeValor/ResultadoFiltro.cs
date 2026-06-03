namespace EmailSpamFilter.Domain.ObjetosDeValor;

public sealed class ResultadoFiltro
{
    public bool EhSpam { get; }
    public string? RegraCorrespondente { get; }
    public string? CampoCorrespondente { get; }

    private ResultadoFiltro(bool ehSpam, string? regra, string? campo)
    {
        EhSpam = ehSpam;
        RegraCorrespondente = regra;
        CampoCorrespondente = campo;
    }

    public static ResultadoFiltro Spam(string regraCorrespondente, string campoCorrespondente)
        => new(true, regraCorrespondente, campoCorrespondente);

    public static ResultadoFiltro Limpo()
        => new(false, null, null);
}
