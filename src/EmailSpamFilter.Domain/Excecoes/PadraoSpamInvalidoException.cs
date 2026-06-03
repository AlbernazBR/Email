namespace EmailSpamFilter.Domain.Excecoes;

public sealed class PadraoSpamInvalidoException : ExcecaoDominio
{
    public PadraoSpamInvalidoException()
        : base("O padrão de spam não pode ser nulo ou vazio.") { }
}
