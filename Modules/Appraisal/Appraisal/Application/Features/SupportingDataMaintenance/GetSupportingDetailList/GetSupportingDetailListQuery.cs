namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDetailList;

public record GetSupportingDetailListQuery(
    int Page,
    int PageSize,
    Guid SupportingId
) : IQuery<GetSupportingDetailListResult>;