namespace Appraisal.Application.Features.Appraisals.GetRentalInfo;

public record GetRentalInfoQuery(
    Guid AppraisalId,
    Guid PropertyId
) : IQuery<GetRentalInfoResult>;
