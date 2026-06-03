using EmailSpamFilter.Domain.ObjetosDeValor;

namespace EmailSpamFilter.Domain.Entidades;

public sealed class MensagemEmail
{
    public string UidImap { get; }
    public string PastaOrigem { get; }
    public EnderecoEmail Remetente { get; }
    public string Assunto { get; }
    public IReadOnlyList<CabecalhoEmail> Cabecalhos { get; }
    public string Corpo { get; }

    public MensagemEmail(
        string uidImap,
        EnderecoEmail remetente,
        string assunto,
        IReadOnlyList<CabecalhoEmail> cabecalhos,
        string corpo,
        string pastaOrigem = "INBOX")
    {
        UidImap = uidImap;
        PastaOrigem = pastaOrigem;
        Remetente = remetente;
        Assunto = assunto;
        Cabecalhos = cabecalhos;
        Corpo = corpo;
    }

    public ResultadoFiltro Analisar(
        IEnumerable<RegraSpam> regras,
        IEnumerable<RegraImpostora>? impostoras = null,
        IEnumerable<RegraRemetenteRegex>? remetenteRegex = null)
    {
        foreach (var regra in regras)
        {
            if (regra.Corresponde(this))
                return ResultadoFiltro.Spam(regra.Padrao.ToString(), regra.CampoCorrespondente(this));
        }

        if (impostoras is not null)
        {
            foreach (var impostor in impostoras)
            {
                if (impostor.Corresponde(this))
                    return ResultadoFiltro.Spam(
                        $"Impostor:{impostor.Palavra}",
                        "Remetente");
            }
        }

        if (remetenteRegex is not null)
        {
            foreach (var regexRegra in remetenteRegex)
            {
                if (regexRegra.Corresponde(this))
                    return ResultadoFiltro.Spam(
                        $"RemetenteGerado:{regexRegra.Padrao}",
                        "Remetente");
            }
        }

        return ResultadoFiltro.Limpo();
    }
}
