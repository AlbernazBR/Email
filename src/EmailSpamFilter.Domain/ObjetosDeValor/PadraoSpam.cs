using EmailSpamFilter.Domain.Enums;
using EmailSpamFilter.Domain.Excecoes;

namespace EmailSpamFilter.Domain.ObjetosDeValor;

public sealed class PadraoSpam : IEquatable<PadraoSpam>
{
    public string Valor { get; }
    public EscopoCorrespondencia Escopo { get; }

    private PadraoSpam(string valor, EscopoCorrespondencia escopo)
    {
        Valor = valor;
        Escopo = escopo;
    }

    public static PadraoSpam Criar(string valor, EscopoCorrespondencia escopo = EscopoCorrespondencia.Global)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new PadraoSpamInvalidoException();

        return new PadraoSpam(valor.Trim().ToLowerInvariant(), escopo);
    }

    public bool Equals(PadraoSpam? outro)
        => outro is not null && Valor == outro.Valor && Escopo == outro.Escopo;

    public override bool Equals(object? obj) => obj is PadraoSpam outro && Equals(outro);
    public override int GetHashCode() => HashCode.Combine(Valor, Escopo);
    public override string ToString() => $"{Escopo}:{Valor}";
}
