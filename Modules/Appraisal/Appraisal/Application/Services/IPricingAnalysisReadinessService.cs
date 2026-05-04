using Appraisal.Domain.Appraisals.Specifications;

namespace Appraisal.Application.Services;

/// <summary>
/// Single entry point for everything pricing-analysis-readiness related.
/// Loads the lightweight <see cref="ReadinessSnapshot"/> and runs every
/// registered <see cref="IPricingAnalysisPrecondition"/> against it.
///
/// Consumers:
///   - Write path: <c>CreatePricingAnalysisCommandHandler</c> and
///     <c>StartPricingAnalysisCommandHandler</c> call <see cref="EvaluateByGroupIdAsync"/>
///     and throw <c>PricingAnalysisNotReadyException</c> on failure.
///   - Read path: <c>GetPropertyGroupByIdQueryHandler</c> calls
///     <see cref="EvaluateByGroupIdAsync"/> and surfaces the result on the response so
///     the React client can disable the "Analyze Price" button.
/// </summary>
public interface IPricingAnalysisReadinessService
{
    /// <summary>
    /// Build a snapshot for the given property group without running the rules.
    /// Useful for diagnostics; most callers want <see cref="EvaluateByGroupIdAsync"/>.
    /// </summary>
    Task<ReadinessSnapshot?> GetSnapshotByGroupIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Build the snapshot and evaluate every precondition. Returns null if the
    /// group does not exist (caller decides whether that is 404 or graceful).
    /// </summary>
    Task<ReadinessResult?> EvaluateByGroupIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);
}
