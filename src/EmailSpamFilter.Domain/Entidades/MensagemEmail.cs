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
        var regraAtivada = regras.FirstOrDefault(r => r.Corresponde(this));
        if (regraAtivada is not null)
            return ResultadoFiltro.Spam(regraAtivada.Padrao.ToString(), regraAtivada.CampoCorrespondente(this));

        var impostor = impostoras?.FirstOrDefault(i => i.Corresponde(this));
        if (impostor is not null)
            return ResultadoFiltro.Spam($"Impostor:{impostor.Palavra}", "Remetente");

        var regexAtivada = remetenteRegex?.FirstOrDefault(r => r.Corresponde(this));
        if (regexAtivada is not null)
            return ResultadoFiltro.Spam($"RemetenteGerado:{regexAtivada.Padrao}", "Remetente");

        return ResultadoFiltro.Limpo();
    }
}
