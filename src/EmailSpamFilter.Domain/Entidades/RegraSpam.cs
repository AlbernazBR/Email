using EmailSpamFilter.Domain.Enums;
using EmailSpamFilter.Domain.Excecoes;
using EmailSpamFilter.Domain.ObjetosDeValor;

namespace EmailSpamFilter.Domain.Entidades;

public sealed class RegraSpam
{
    public Guid Id { get; }
    public PadraoSpam Padrao { get; }
    public EscopoCorrespondencia Escopo { get; }
    public bool Ativa { get; private set; }

    private RegraSpam(Guid id, PadraoSpam padrao, EscopoCorrespondencia escopo)
    {
        Id = id;
        Padrao = padrao;
        Escopo = escopo;
        Ativa = true;
    }

    public static RegraSpam Criar(PadraoSpam padrao, EscopoCorrespondencia escopo)
        => new(Guid.NewGuid(), padrao, escopo);

    public bool Corresponde(MensagemEmail mensagem)
    {
        if (!Ativa) return false;

        return Escopo switch
        {
            EscopoCorrespondencia.ApenasRemetente => CorrespondeNoRemetente(mensagem),
            EscopoCorrespondencia.ApenasCabecalho => CorrespondeNoCabecalho(mensagem),
            EscopoCorrespondencia.Global          => CorrespondeNoRemetente(mensagem)
                                                  || CorrespondeNoCabecalho(mensagem)
                                                  || CorrespondeNoConteudo(mensagem),
            _ => false
        };
    }

    private bool CorrespondeNoRemetente(MensagemEmail mensagem)
        => mensagem.Remetente.Contem(Padrao.Valor);

    private bool CorrespondeNoCabecalho(MensagemEmail mensagem)
        => mensagem.Cabecalhos.Any(c =>
               c.Valor.Contains(Padrao.Valor, StringComparison.OrdinalIgnoreCase) ||
               c.Nome.Contains(Padrao.Valor, StringComparison.OrdinalIgnoreCase));

    private bool CorrespondeNoConteudo(MensagemEmail mensagem)
        => mensagem.Assunto.Contains(Padrao.Valor, StringComparison.OrdinalIgnoreCase)
        || mensagem.Corpo.Contains(Padrao.Valor, StringComparison.OrdinalIgnoreCase);

    public string CampoCorrespondente(MensagemEmail mensagem)
    {
        if (CorrespondeNoRemetente(mensagem)) return "Remetente";
        if (CorrespondeNoCabecalho(mensagem)) return "Cabeçalho";
        if (CorrespondeNoConteudo(mensagem))  return "Conteúdo";
        return "Desconhecido";
    }

    public void Desativar() => Ativa = false;
}
