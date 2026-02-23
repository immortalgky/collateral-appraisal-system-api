namespace Appraisal.Application.Features.Appraisals.CreateAppraisal;

public record CreateAppraisalRequest(
    Guid RequestId,
    string AppraisalType,
    string Priority,
    int? SLADays = null
);