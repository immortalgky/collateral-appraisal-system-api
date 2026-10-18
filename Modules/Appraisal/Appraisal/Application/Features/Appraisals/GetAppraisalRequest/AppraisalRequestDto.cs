using Request.Contracts.RequestDocuments.Dto;
using Request.Contracts.Requests.Dtos;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalRequest;

/// <summary>
/// An appraisal's request data (<c>request</c> of GET /appraisals/{id}?include=request), shaped to pre-fill
/// a new CreateRequest form. Appointment and Fee are deliberately excluded; the request's files are the
/// separate <c>documents</c> include.
/// </summary>
/// <param name="PrevAppraisal">
/// THIS appraisal's own prior book - the prior appraisal its request referenced (book 1 for book 2) - or null when
/// the request had none. Not a snapshot of the appraisal being read: the header already is that, and a new request
/// copying this appraisal stamps its own prior from the header.
/// </param>
public record AppraisalRequestDto(
    PrevAppraisalDto? PrevAppraisal,
    RequestDetailCopyDto Detail,
    List<RequestCustomerDto> Customers,
    List<RequestPropertyDto> Properties,
    List<RequestTitleDto> Titles
);

/// <summary>
/// The prior book a request referenced, as stored on its RequestDetail. A book that exists in CAS has an id; a legacy
/// AS400 (99A...) book has the number only (and the value / date LOS or AS400 sent, when there were any).
/// </summary>
public record PrevAppraisalDto(
    Guid? AppraisalId,
    string? AppraisalNumber,
    decimal? AppraisalValue,
    // The appraisal (valuation) date - what "วันที่ประเมินครั้งก่อน" means on the request form.
    DateTime? AppraisalDate
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
