using Parameter.Contracts.Parameters.Dtos;

namespace Parameter.Contracts.Parameters;

public interface IParameterLookupService
{
    Task<IReadOnlySet<string>> GetValidCodesAsync(
        ParameterDto parameter,
        CancellationToken cancellationToken
        );
}