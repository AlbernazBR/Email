using EmailSpamFilter.Domain.Excecoes;

namespace EmailSpamFilter.Domain.Entidades;

/// <summary>
/// Detecta impostores: emails que mencionam uma marca conhecida
/// mas não foram enviados pelo domínio legítimo dessa marca.
/// Exemplo: assunto "Bradesco" + remetente fora de @bradesco.com.br → spam.
/// </summary>
public sealed class RegraImpostora
{
    public string Palavra { get; }
    public string DominioLegitimo { get; }

    private RegraImpostora(string palavra, string dominioLegitimo)
    {
        Palavra = palavra;
        DominioLegitimo = dominioLegitimo;
    }

    public static RegraImpostora Criar(string palavra, string dominioLegitimo)
    {
        if (string.IsNullOrWhiteSpace(palavra))
            throw new ExcecaoDominioException("Palavra da regra de impostor não pode ser vazia.");

        if (string.IsNullOrWhiteSpace(dominioLegitimo))
            throw new ExcecaoDominioException("Domínio legítimo não pode ser vazio.");

        return new RegraImpostora(
            palavra.Trim().ToLowerInvariant(),
            dominioLegitimo.Trim().ToLowerInvariant());
    }

    public bool Corresponde(MensagemEmail mensagem)
    {
        var enderecoReal = mensagem.Remetente.Valor;
        var nomeExibido  = mensagem.Remetente.NomeExibido;

        // Menciona a marca no endereço real, no nome exibido ou no assunto?
        bool mencionaMarca = enderecoReal.Contains(Palavra, StringComparison.OrdinalIgnoreCase)
                          || nomeExibido.Contains(Palavra, StringComparison.OrdinalIgnoreCase)
                          || mensagem.Assunto.Contains(Palavra, StringComparison.OrdinalIgnoreCase);

        if (!mencionaMarca) return false;

        // Vem do domínio legítimo?
        bool dominioOk = enderecoReal.EndsWith("@" + DominioLegitimo, StringComparison.OrdinalIgnoreCase)
                      || enderecoReal.EndsWith("." + DominioLegitimo, StringComparison.OrdinalIgnoreCase);

        // Menciona a marca MAS não vem do domínio legítimo → impostor
        return !dominioOk;
    }
}
