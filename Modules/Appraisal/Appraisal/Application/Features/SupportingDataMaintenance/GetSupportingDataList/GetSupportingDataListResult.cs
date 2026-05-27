namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataList;

public record GetSupportingDataListResult(IEnumerable<SupportingDataListItem> SupportingDataList, bool HasAuthorityToCreate, int TotalCount, int PageNumber, int PageSize);

public record SupportingDataListItem(
    Guid Id,
    string? SupportingNumber,
    string Status,
    string? ImportChannel,
    DateTime? ImportDate,
    string? SourceOfData,
    string? AppraisalCompany,
    string? Description,
    string? Remark
);