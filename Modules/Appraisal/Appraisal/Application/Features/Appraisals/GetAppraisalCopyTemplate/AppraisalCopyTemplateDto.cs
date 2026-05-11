using Request.Contracts.RequestDocuments.Dto;
using Request.Contracts.Requests.Dtos;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalCopyTemplate;

/// <summary>
/// Snapshot of a completed appraisal's request data, shaped to pre-fill
/// a new CreateRequest form. Appointment and Fee are deliberately excluded.
/// </summary>
public record AppraisalCopyTemplateDto(
    PrevAppraisalSnapshotDto PrevAppraisal,
    RequestDetailCopyDto Detail,
    List<RequestCustomerDto> Customers,
    List<RequestPropertyDto> Properties,
    List<RequestTitleDto> Titles,
    List<RequestDocumentDto> Documents
);

/// <summary>
/// Metadata about the source appraisal, used to stamp PrevAppraisal* fields
/// on the new RequestDetail without a second round-trip.
/// </summary>
public record PrevAppraisalSnapshotDto(
    Guid AppraisalId,
    string? AppraisalNumber,
    decimal? AppraisalValue,
    DateTime? AppointmentDate
);

/// <summary>
/// The copyable portion of RequestDetail — address, contact, loanDetail only.
/// Appointment and Fee are NOT included.
/// Field names mirror RequestDetailDto exactly so the FE mapper is trivial.
/// </summary>
public record RequestDetailCopyDto(
    bool HasAppraisalBook,
    LoanDetailDto? LoanDetail,
    AddressDto? Address,
    ContactDto? Contact
);
