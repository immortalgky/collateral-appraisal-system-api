namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataById;

public record GetSupportingDataByIdResponse(
    Guid Id,
    string? SupportingNumber,
    bool HasAuthorityToEdit,
    bool HasAuthorityToDecision,
    string Status,
    string? ImportChannel,
    DateTime? ImportDate,
    string? SourceOfData,
    string? AppraisalCompany,
    string? Description,
    string? Remark) : IQuery<GetSupportingDataByIdResult>;