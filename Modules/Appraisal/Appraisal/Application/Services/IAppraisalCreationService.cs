using Request.Contracts.Requests.Dtos;

namespace Appraisal.Application.Services;

/// <summary>
/// Service for creating appraisals from request submissions.
/// Handles the business logic for appraisal creation workflow.
/// </summary>
public interface IAppraisalCreationService
{
    /// <summary>
    /// Creates an appraisal from a submitted request with its titles.
    /// Also creates an initial unassigned assignment, fee (from FeeStructure), and appointment (if provided).
    /// </summary>
    Task<Guid> CreateAppraisalFromRequest(
        Guid requestId,
        List<RequestTitleDto> requestTitles,
        AppointmentDto? appointment = null,
        FeeDto? fee = null,
        ContactDto? contact = null,
        string? createdBy = null,
        string? priority = null,
        bool isPma = false,
        string? purpose = null,
        string? channel = null,
        string? bankingSegment = null,
        decimal? facilityLimit = null,
        CancellationToken cancellationToken = default);
}