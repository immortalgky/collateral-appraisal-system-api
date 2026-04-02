namespace Appraisal.Application.Features.Appraisals.GetRentalSchedule;

public record GetRentalScheduleQuery(
    Guid AppraisalId,
    Guid PropertyId
) : IQuery<GetRentalScheduleResult>;
