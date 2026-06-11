namespace Request.Application.Services;

/// <summary>
/// Generates sequential reappraisal group numbers (e.g. "68G000001").
/// Used by CreateBlockReappraisalCommandHandler for the block reappraisal path.
/// The AS400 COLLATREV path uses the Collateral module's equivalent.
/// </summary>
public interface IReappraisalGroupNumberGenerator
{
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}
