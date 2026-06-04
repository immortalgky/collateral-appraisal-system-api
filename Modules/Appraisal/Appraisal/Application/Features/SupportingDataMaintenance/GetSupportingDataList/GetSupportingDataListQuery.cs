namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataList;

public record GetSupportingDataListQuery(
    int Page,
    int PageSize,
    string? Status,
    DateTime? DateFrom,
    DateTime? DateTo,
    DateTime? LastModifiedDateFrom,
    DateTime? LastModifiedDateTo,
    string? SupportingNumber,
    string? Search,
    string? SortBy,
    string? SortDir
) : IQuery<GetSupportingDataListResult>;