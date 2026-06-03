namespace EmailSpamFilter.Domain.Excecoes;

public class ExcecaoDominio : Exception
{
    public ExcecaoDominio(string mensagem) : base(mensagem) { }
    public ExcecaoDominio(string mensagem, Exception inner) : base(mensagem, inner) { }
}
