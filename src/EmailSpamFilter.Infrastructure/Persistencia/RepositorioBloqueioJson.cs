using EmailSpamFilter.Domain.Agregados;
using EmailSpamFilter.Domain.Entidades;
using EmailSpamFilter.Domain.Enums;
using EmailSpamFilter.Domain.Excecoes;
using EmailSpamFilter.Domain.Interfaces;
using EmailSpamFilter.Domain.ObjetosDeValor;
using EmailSpamFilter.Infrastructure.Configuracao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace EmailSpamFilter.Infrastructure.Persistencia;

public sealed class RepositorioBloqueioJson : IRepositorioBloqueio
{
    private readonly string _caminhoArquivo;
    private readonly ILogger<RepositorioBloqueioJson> _logger;

    private RegrasBloqueio? _cache;
    private DateTime _carregadoEm = DateTime.MinValue;

    public RepositorioBloqueioJson(
        IOptions<ConfiguracoesFiltro> options,
        ILogger<RepositorioBloqueioJson> logger)
    {
        _caminhoArquivo = options.Value.ArquivoBloqueio;
        _logger = logger;
    }

    public Task<RegrasBloqueio> CarregarAsync(CancellationToken ct)
    {
        if (!File.Exists(_caminhoArquivo))
        {
            _logger.LogWarning("Arquivo de bloqueio não encontrado: {Arquivo}. Retornando lista vazia.",
                _caminhoArquivo);
            return Task.FromResult(new RegrasBloqueio());
        }

        var ultimaEscrita = File.GetLastWriteTimeUtc(_caminhoArquivo);
        if (_cache is not null && ultimaEscrita <= _carregadoEm)
            return Task.FromResult(_cache);

        _cache = CarregarDoArquivo();
        _carregadoEm = ultimaEscrita;

        return Task.FromResult(_cache);
    }

    private RegrasBloqueio CarregarDoArquivo()
    {
        var json = File.ReadAllText(_caminhoArquivo);
        var dto = JsonSerializer.Deserialize<BloqueioDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new BloqueioDto();

        var regras = new RegrasBloqueio();

        foreach (var p in dto.Padroes)
            AdicionarRegra(regras, p, EscopoCorrespondencia.Global);

        foreach (var p in dto.PadroesPorRemetente)
            AdicionarRegra(regras, p, EscopoCorrespondencia.ApenasRemetente);

        foreach (var p in dto.PadroesPorCabecalho)
            AdicionarRegra(regras, p, EscopoCorrespondencia.ApenasCabecalho);

        foreach (var i in dto.Impostores)
        {
            try
            {
                regras.AdicionarImpostora(RegraImpostora.Criar(i.Palavra, i.DominioLegitimo));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Impostor inválido ignorado '{P}': {Erro}", i.Palavra, ex.Message);
            }
        }

        foreach (var p in dto.PadroesDominioSuspeito)
        {
            try
            {
                regras.AdicionarRemetenteRegex(RegraRemetenteRegex.Criar(p));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Regex de remetente inválido ignorado '{P}': {Erro}", p, ex.Message);
            }
        }

        foreach (var p in dto.RemetentesPermitidos)
        {
            try
            {
                regras.AdicionarPermitidoRemetente(p);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Remetente permitido inválido ignorado '{P}': {Erro}", p, ex.Message);
            }
        }

        _logger.LogInformation(
            "Bloqueio carregado — Global: {G}, Remetente: {R}, Cabeçalho: {C}, Impostores: {I}, Regex: {X}, Permitidos: {P}",
            dto.Padroes.Count, dto.PadroesPorRemetente.Count, dto.PadroesPorCabecalho.Count,
            dto.Impostores.Count, dto.PadroesDominioSuspeito.Count, dto.RemetentesPermitidos.Count);

        return regras;
    }

    private void AdicionarRegra(RegrasBloqueio regras, string valor, EscopoCorrespondencia escopo)
    {
        try
        {
            var padrao = PadraoSpam.Criar(valor, escopo);
            regras.AdicionarRegra(RegraSpam.Criar(padrao, escopo));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Padrão inválido ignorado '{Valor}': {Erro}", valor, ex.Message);
        }
    }

    private sealed class BloqueioDto
    {
        public List<string> Padroes { get; set; } = [];
        public List<string> PadroesPorRemetente { get; set; } = [];
        public List<string> PadroesPorCabecalho { get; set; } = [];
        public List<ImpostorDto> Impostores { get; set; } = [];
        public List<string> PadroesDominioSuspeito { get; set; } = [];
        public List<string> RemetentesPermitidos { get; set; } = [];
    }

    private sealed class ImpostorDto
    {
        public string Palavra { get; set; } = string.Empty;
        public string DominioLegitimo { get; set; } = string.Empty;
    }
}
