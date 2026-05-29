namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDetailById;

public record GetSupportingDetailByIdQuery(
    Guid SupportingId,
    Guid DetailId
) : IQuery<GetSupportingDetailByIdResult>;
