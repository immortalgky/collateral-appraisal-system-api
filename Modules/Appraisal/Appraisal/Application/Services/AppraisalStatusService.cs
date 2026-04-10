using Appraisal.Domain.Appraisals;
using Appraisal.Contracts.Services;

namespace Appraisal.Application.Services;

/// <summary>
/// Implements cross-module status updates for the Workflow module's submission pipeline.
/// Maps target status strings to domain methods on the Appraisal aggregate.
/// </summary>
public class AppraisalStatusService(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork) : IAppraisalStatusService
{
    public async Task UpdateStatusAsync(Guid appraisalId, string targetStatus, string updatedBy, CancellationToken ct)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(appraisalId, ct)
                        ?? throw new InvalidOperationException($"Appraisal {appraisalId} not found");

        switch (targetStatus)
        {
            case "IN_PROGRESS":
            case "InProgress":
                appraisal.StartWork();
                break;
            case "UNDER_REVIEW":
            case "UnderReview":
                appraisal.SubmitForReview();
                break;
            case "COMPLETED":
            case "Completed":
                appraisal.Complete();
                break;
            default:
                throw new ArgumentException($"Unsupported target status: {targetStatus}");
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task UpdateAssignmentStatusAsync(Guid appraisalId, string targetStatus, string updatedBy,
        CancellationToken ct)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(appraisalId, ct)
                        ?? throw new InvalidOperationException($"Appraisal {appraisalId} not found");

        var activeAssignment = appraisal.Assignments
            .FirstOrDefault(a =>
                a.AssignmentStatus == AssignmentStatus.Assigned ||
                a.AssignmentStatus == AssignmentStatus.InProgress);

        if (activeAssignment is null)
            throw new InvalidOperationException($"No active assignment found for appraisal {appraisalId}");

        switch (targetStatus)
        {
            case "IN_PROGRESS":
            case "InProgress":
                activeAssignment.StartWork();
                break;
            case "COMPLETED":
            case "Completed":
                activeAssignment.Complete();
                break;
            default:
                throw new ArgumentException($"Unsupported assignment target status: {targetStatus}");
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}
