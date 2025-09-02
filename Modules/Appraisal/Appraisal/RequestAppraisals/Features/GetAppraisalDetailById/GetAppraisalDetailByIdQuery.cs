namespace Appraisal.RequestAppraisals.Features.GetAppraisalDetailById;

public record GetAppraisalDetailByIdQuery(long Id) : IQuery<GetAppraisalDetailByIdResult>;