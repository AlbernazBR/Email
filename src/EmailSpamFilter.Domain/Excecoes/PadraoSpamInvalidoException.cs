namespace EmailSpamFilter.Domain.Excecoes;

public sealed class PadraoSpamInvalidoException : ExcecaoDominioException
{
    public PadraoSpamInvalidoException()
        : base("O padrão de spam não pode ser nulo ou vazio.") { }
}
