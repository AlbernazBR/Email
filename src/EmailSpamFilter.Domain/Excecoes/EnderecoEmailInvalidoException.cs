namespace EmailSpamFilter.Domain.Excecoes;

public sealed class EnderecoEmailInvalidoException : ExcecaoDominio
{
    public EnderecoEmailInvalidoException(string valor)
        : base($"Endereço de e-mail inválido: '{valor}'") { }
}
