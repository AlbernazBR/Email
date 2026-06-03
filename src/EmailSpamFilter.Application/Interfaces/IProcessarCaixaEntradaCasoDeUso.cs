using EmailSpamFilter.Application.DTOs;

namespace EmailSpamFilter.Application.Interfaces;

public interface IProcessarCaixaEntradaCasoDeUso
{
    Task<ResultadoProcessamento> ExecutarAsync(OpcoesProcessamento opcoes, CancellationToken ct);
}
