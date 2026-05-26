namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataById;

public record GetSupportingDataByIdResult(
    Guid Id,
    string SupportingNumber,
    string Status,
    string ImportChannel,
    DateTime ImportDate,
    string SourceOfData,
    string AppraisalCompany,
    string Description,
    string Remark
);