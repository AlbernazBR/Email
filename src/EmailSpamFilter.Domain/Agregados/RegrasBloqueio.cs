using EmailSpamFilter.Domain.Entidades;
using EmailSpamFilter.Domain.Excecoes;
using EmailSpamFilter.Domain.ObjetosDeValor;

namespace EmailSpamFilter.Domain.Agregados;

public sealed class RegrasBloqueio
{
    private readonly List<RegraSpam> _regras = [];
    private readonly List<RegraImpostora> _impostoras = [];
    private readonly List<RegraRemetenteRegex> _remetenteRegex = [];

    public IReadOnlyList<RegraSpam> ObterRegrasAtivas()
        => _regras.Where(r => r.Ativa).ToList().AsReadOnly();

    public IReadOnlyList<RegraImpostora> ObterImpostoras()
        => _impostoras.AsReadOnly();

    public IReadOnlyList<RegraRemetenteRegex> ObterRemetenteRegex()
        => _remetenteRegex.AsReadOnly();

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

    public void RemoverRegra(Guid id)
    {
        var regra = _regras.FirstOrDefault(r => r.Id == id)
            ?? throw new ExcecaoDominioException($"Regra com id '{id}' não encontrada.");

        regra.Desativar();
    }
}
