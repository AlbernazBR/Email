using System.Text.RegularExpressions;
using EmailSpamFilter.Domain.Excecoes;

namespace EmailSpamFilter.Domain.Entidades;

/// <summary>
/// Detecta remetentes gerados por software: domínios com IDs numéricos,
/// prefixos aleatórios, dashes triplos, etc.
/// Aplica um regex ao endereço real + nome exibido do remetente.
/// </summary>
public sealed class RegraRemetenteRegex
{
    private readonly Regex _regex;

    public string Padrao { get; }

    private RegraRemetenteRegex(string padrao, Regex regex)
    {
        Padrao = padrao;
        _regex = regex;
    }

    public static RegraRemetenteRegex Criar(string padrao)
    {
        if (string.IsNullOrWhiteSpace(padrao))
            throw new ExcecaoDominio("Padrão de remetente regex não pode ser vazio.");

        try
        {
            var regex = new Regex(
                padrao,
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
                TimeSpan.FromMilliseconds(100));

            return new RegraRemetenteRegex(padrao, regex);
        }
        catch (ArgumentException ex)
        {
            throw new ExcecaoDominio($"Padrão regex inválido '{padrao}': {ex.Message}");
        }
    }

    /// <summary>
    /// Verifica o endereço real e o nome exibido do remetente.
    /// </summary>
    public bool Corresponde(MensagemEmail mensagem)
    {
        var textoParaVerificar = mensagem.Remetente.Valor
                               + " "
                               + mensagem.Remetente.NomeExibido;

        return _regex.IsMatch(textoParaVerificar);
    }
}
