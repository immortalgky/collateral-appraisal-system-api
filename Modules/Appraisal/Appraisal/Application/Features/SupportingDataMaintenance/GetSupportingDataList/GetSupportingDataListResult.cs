namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataList;

public record GetSupportingDataListResult(
    IEnumerable<SupportingDataListItem> SupportingDataList,
    bool HasAuthorityToRemove,
    bool HasAuthorityToEdit,
    int TotalCount,
    int PageNumber,
    int PageSize
);

public record SupportingDataListItem(
    Guid Id,
    string? SupportingNumber,
    string Status,
    string? ImportChannel,
    DateTime? ImportDate,
    string? SourceOfData,
    Guid? AppraisalCompanyId,
    string? Description,
    string? Remark,
    DateTime? CreatedDate,
    DateTime? LastModifiedDate,
    string? LastModifiedBy
);