namespace EmailSpamFilter.Domain.Excecoes;

public class ExcecaoDominioException : Exception
{
    public ExcecaoDominioException(string mensagem) : base(mensagem) { }
    public ExcecaoDominioException(string mensagem, Exception inner) : base(mensagem, inner) { }
}
