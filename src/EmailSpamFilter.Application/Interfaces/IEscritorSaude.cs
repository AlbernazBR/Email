using EmailSpamFilter.Application.DTOs;

namespace EmailSpamFilter.Application.Interfaces;

public interface IEscritorSaude
{
    Task RegistrarAsync(InstantaneoSaude instantaneo, CancellationToken ct);
}