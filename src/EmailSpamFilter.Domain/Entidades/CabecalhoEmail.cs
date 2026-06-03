namespace EmailSpamFilter.Domain.Entidades;

public sealed class CabecalhoEmail
{
    public string Nome { get; }
    public string Valor { get; }

    public CabecalhoEmail(string nome, string valor)
    {
        Nome = nome;
        Valor = valor;
    }

    public override string ToString() => $"{Nome}: {Valor}";
}
