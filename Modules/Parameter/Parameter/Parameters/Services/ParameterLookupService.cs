using Parameter.Contracts.Parameters;

namespace Parameter.Parameters.Services;

public class ParameterLookupService(IParameterRepository parameterRepository) : IParameterLookupService
{
    public async Task<IReadOnlySet<string>> GetValidCodesAsync(
        ParameterDto query,
        CancellationToken ct
    )
    {
        var parameters = await parameterRepository.GetParameter(query, cancellationToken: ct);
        return parameters.Select(p => p.Code).ToHashSet();
    }

    public async Task<string?> GetDescriptionAsync(
        ParameterDto query,
        CancellationToken ct
    )
    {
        var parameters = await parameterRepository.GetParameter(query, cancellationToken: ct);
        return parameters.FirstOrDefault()?.Description;
    }
}