namespace Collateral.CollateralMasters.Reappraisal.Services;

/// <summary>
/// Generates a sequential group number for a batch of reappraisal requests.
/// Format: {YY}G{000001} e.g. "68G000001".
/// </summary>
public interface IReappraisalGroupNumberGenerator
{
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}
