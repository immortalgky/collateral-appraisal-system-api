namespace Appraisal.Contracts.Services;

/// <summary>
/// Cross-module contract for updating appraisal and assignment statuses.
/// Implemented in the Appraisal module, consumed by the Workflow module.
/// </summary>
public interface IAppraisalStatusService
{
    Task UpdateStatusAsync(Guid appraisalId, string targetStatus, string updatedBy, CancellationToken ct);
    Task UpdateAssignmentStatusAsync(Guid appraisalId, string targetStatus, string updatedBy, CancellationToken ct);
}
