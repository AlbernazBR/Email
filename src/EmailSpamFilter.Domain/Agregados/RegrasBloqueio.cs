using EmailSpamFilter.Domain.Entidades;
using EmailSpamFilter.Domain.Excecoes;
using EmailSpamFilter.Domain.ObjetosDeValor;

namespace EmailSpamFilter.Domain.Agregados;

public sealed class RegrasBloqueio
{
    private readonly List<RegraSpam> _regras = [];
    private readonly List<RegraImpostora> _impostoras = [];
    private readonly List<RegraRemetenteRegex> _remetenteRegex = [];
    private readonly List<string> _permitidosRemetente = [];

    public IReadOnlyList<RegraSpam> ObterRegrasAtivas()
        => _regras.Where(r => r.Ativa).ToList().AsReadOnly();

    public IReadOnlyList<RegraImpostora> ObterImpostoras()
        => _impostoras.AsReadOnly();

    public IReadOnlyList<RegraRemetenteRegex> ObterRemetenteRegex()
        => _remetenteRegex.AsReadOnly();

    public IReadOnlyList<string> ObterPermitidosRemetente()
        => _permitidosRemetente.AsReadOnly();

    public void AdicionarRegra(RegraSpam regra)
    {
        if (_regras.Any(r => r.Padrao.Equals(regra.Padrao) && r.Ativa))
            throw new ExcecaoDominioException(
                $"Já existe uma regra ativa para o padrão '{regra.Padrao}'.");

        _regras.Add(regra);
    }

    public void AdicionarImpostora(RegraImpostora regra)
    {
        if (_impostoras.Any(r => r.Palavra == regra.Palavra && r.DominioLegitimo == regra.DominioLegitimo))
            return;

        _impostoras.Add(regra);
    }

    public void AdicionarRemetenteRegex(RegraRemetenteRegex regra)
    {
        if (_remetenteRegex.Any(r => r.Padrao == regra.Padrao))
            return;

        _remetenteRegex.Add(regra);
    }

    public void AdicionarPermitidoRemetente(string permitido)
    {
        if (string.IsNullOrWhiteSpace(permitido))
            throw new ExcecaoDominioException("Remetente permitido não pode ser vazio.");

        var normalizado = permitido.Trim().ToLowerInvariant();
        if (_permitidosRemetente.Contains(normalizado))
            return;

        _permitidosRemetente.Add(normalizado);
    }

    public bool EhRemetentePermitido(string remetente)
    {
        if (string.IsNullOrWhiteSpace(remetente))
            return false;

        var remetenteNormalizado = remetente.Trim().ToLowerInvariant();
        var dominioRemetente = ExtrairDominio(remetenteNormalizado);

        return _permitidosRemetente.Any(permitido =>
            EhEmailExato(permitido, remetenteNormalizado)
            || EhDominioPermitido(permitido, dominioRemetente));
    }

    private static bool EhEmailExato(string permitido, string remetenteNormalizado)
        => permitido.Contains('@')
           && string.Equals(permitido, remetenteNormalizado, StringComparison.OrdinalIgnoreCase);

    private static bool EhDominioPermitido(string permitido, string? dominioRemetente)
    {
        if (dominioRemetente is null || permitido.Contains('@'))
            return false;

        return string.Equals(dominioRemetente, permitido, StringComparison.OrdinalIgnoreCase)
            || dominioRemetente.EndsWith('.' + permitido, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtrairDominio(string remetenteNormalizado)
    {
        var indiceArroba = remetenteNormalizado.LastIndexOf('@');
        if (indiceArroba < 0 || indiceArroba == remetenteNormalizado.Length - 1)
            return null;

        return remetenteNormalizado[(indiceArroba + 1)..];
    }

    public void RemoverRegra(Guid id)
    {
        var regra = _regras.FirstOrDefault(r => r.Id == id)
            ?? throw new ExcecaoDominioException($"Regra com id '{id}' não encontrada.");

        regra.Desativar();
    }
}
