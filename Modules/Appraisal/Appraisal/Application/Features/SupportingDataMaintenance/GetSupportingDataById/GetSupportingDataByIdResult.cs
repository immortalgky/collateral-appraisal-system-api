namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataById;

public record GetSupportingDataByIdResult(
    Guid Id,
    string? SupportingNumber,
    bool HasAuthorityToEdit,
    bool HasAuthorityToDecision,
    string Status,
    string? ImportChannel,
    DateTime? ImportDate,
    string? SourceOfData,
    Guid? AppraisalCompanyId,
    string? Description,
    string? Remark
);