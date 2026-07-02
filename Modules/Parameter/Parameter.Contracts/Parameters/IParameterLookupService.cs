using Parameter.Contracts.Parameters.Dtos;

namespace Parameter.Contracts.Parameters;

public interface IParameterLookupService
{
    Task<IReadOnlySet<string>> GetValidCodesAsync(
        ParameterDto parameter,
        CancellationToken cancellationToken
        );

    /// <summary>
    /// Resolves the description for the parameter matching the given query (Group/Code/Language…).
    /// Returns the first match's description, or null when no parameter matches.
    /// </summary>
    Task<string?> GetDescriptionAsync(
        ParameterDto parameter,
        CancellationToken cancellationToken
        );
}