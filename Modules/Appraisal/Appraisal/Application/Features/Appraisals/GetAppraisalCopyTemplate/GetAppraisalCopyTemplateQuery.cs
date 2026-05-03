namespace Appraisal.Application.Features.Appraisals.GetAppraisalCopyTemplate;

/// <summary>
/// Returns a copyable snapshot of a completed appraisal's request data.
/// Raises 404 if the appraisal does not exist; 409 if it is not Completed.
/// </summary>
public record GetAppraisalCopyTemplateQuery(Guid AppraisalId) : IQuery<AppraisalCopyTemplateDto>;
