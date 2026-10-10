namespace Appraisal.Application.Features.Appraisals.GetNextInspectionNumber;

/// <summary>
/// The inspection round a NEW Construction-Inspection request copying this appraisal would be: the non-cancelled
/// Progressive inspections already in this appraisal's chain + 1 - the very value AppraisalCreationService stamps when
/// the appraisal is created, so a preview matches it. Raises 404 if the appraisal does not exist.
/// </summary>
public record GetNextInspectionNumberQuery(Guid AppraisalId) : IQuery<GetNextInspectionNumberResult>;

public record GetNextInspectionNumberResult(int NextInspectionNumber);
