namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataList;

public record GetSupportingDataListResult(IEnumerable<SupportingDataListItem> SupportingDataList, int TotalCount);

public record SupportingDataListItem(
    Guid Id,
    string SupportingNumber,
    string ImportChannel,
    DateTime ImportDate,
    string SourceOfData,
    string? AppraisalCompany,
    string? Description,
    string? Remark
);