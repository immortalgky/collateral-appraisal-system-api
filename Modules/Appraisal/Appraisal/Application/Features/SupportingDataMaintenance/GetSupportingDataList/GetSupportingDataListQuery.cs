namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataList;

public record GetSupportingDataListQuery(
    int Page,
    int PageSize,
    string? Status,
    DateTime? ImportDate,
    string? SupportingNumber
) : IQuery<GetSupportingDataListResult>;