namespace Appraisal.Application.Features.Appraisals.GetAppraisalRequest;

/// <summary>
/// The appraisal's request data, shaped to pre-fill a new CreateRequest form: read by
/// GET /appraisals/{id}?include=request. Returns the data whatever the appraisal's status; the consumer
/// checks the status from the header. Raises 404 if the appraisal does not exist.
/// </summary>
public record GetAppraisalRequestQuery(Guid AppraisalId) : IQuery<AppraisalRequestDto>;
