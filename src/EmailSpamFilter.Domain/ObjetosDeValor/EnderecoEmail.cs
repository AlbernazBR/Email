using EmailSpamFilter.Domain.Excecoes;

namespace EmailSpamFilter.Domain.ObjetosDeValor;

public sealed class EnderecoEmail : IEquatable<EnderecoEmail>
{
    public string Valor { get; }

    /// <summary>Nome exibido pelo cliente de email (ex: "Central de Relacionamento Bradesco").</summary>
    public string NomeExibido { get; }

    private EnderecoEmail(string valor, string nomeExibido)
    {
        Valor = valor;
        NomeExibido = nomeExibido;
    }

    public static EnderecoEmail Criar(string bruto, string nomeExibido = "")
    {
        if (string.IsNullOrWhiteSpace(bruto) || !bruto.Contains('@'))
            throw new EnderecoEmailInvalidoException(bruto ?? string.Empty);

        return new EnderecoEmail(
            bruto.Trim().ToLowerInvariant(),
            nomeExibido.Trim());
    }

    public bool Contem(string texto)
        => Valor.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || NomeExibido.Contains(texto, StringComparison.OrdinalIgnoreCase);

    public bool Equals(EnderecoEmail? outro) => outro is not null && Valor == outro.Valor;
    public override bool Equals(object? obj) => obj is EnderecoEmail outro && Equals(outro);
    public override int GetHashCode() => Valor.GetHashCode(StringComparison.OrdinalIgnoreCase);
    public override string ToString() => Valor;
}
