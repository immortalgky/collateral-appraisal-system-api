namespace Appraisal.Application.Features.Appraisals.UpdateInspectionNumber;

/// <summary>
/// Corrects the construction-inspection round number of one Progressive appraisal.
/// Affects only this appraisal; the next round is still ProgressiveCount + 1.
/// </summary>
public record UpdateInspectionNumberCommand(Guid AppraisalId, int InspectionNumber)
    : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
