using Appraisal.Contracts.Appraisals;
using Shared.Exceptions;

namespace Request.Application.Services;

/// <summary>
/// Submit-time gate for request purposes that CONTINUE a prior appraisal:
///   - Appeal              → Purpose "12"
///   - Progressive (CI)    → Purpose "06" / "11"
///
/// These flows resolve company/copy/fee from a prior appraisal, so the prior must exist and be
/// Completed before submission — otherwise the downstream resolution has nothing sound to build on
/// and would silently degrade (empty copy / round-robin / no fee). Throws BadRequestException
/// (HTTP 400) so the failure surfaces in front of the user before any workflow/appraisal is created.
/// </summary>
internal static class PriorAppraisalSubmissionGuard
{
    private static readonly HashSet<string> PriorAppraisalRequiredPurposes =
        new(StringComparer.Ordinal) { "06", "11", "12" };

    // Cross-module string contract for AppraisalStatus.Completed (Appraisal.Domain is not referenced here).
    private const string CompletedStatus = "Completed";

    public static async Task EnsureValidAsync(
        string? purpose,
        Guid? prevAppraisalId,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        if (purpose is null || !PriorAppraisalRequiredPurposes.Contains(purpose))
            return;

        if (!prevAppraisalId.HasValue)
            throw new BadRequestException("A prior appraisal is required for this request purpose.");

        var prior = await mediator.Send(new GetAppraisalReferenceQuery(prevAppraisalId.Value), cancellationToken);

        if (prior is null)
            throw new BadRequestException("The referenced prior appraisal was not found.");

        if (!string.Equals(prior.Status, CompletedStatus, StringComparison.Ordinal))
            throw new BadRequestException(
                "The referenced prior appraisal must be completed before this request can be submitted.");
    }
}
