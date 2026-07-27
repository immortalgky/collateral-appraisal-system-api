namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

/// <summary>
/// Filter request for GetAppraisals query.
/// Supports text search, multi-value filters, date ranges, geographic, and sorting.
/// </summary>
public record GetAppraisalsFilterRequest(
    // Text search (matches AppraisalNumber or CustomerName)
    string? Search = null,

    // Exact / multi-value filters (comma-separated for IN)
    string? Status = null,
    string? Priority = null,
    string? AppraisalType = null,
    string? SlaStatus = null,
    string? AssignmentType = null,

    // Assignment filters (AssigneeUserId stores username like "P5229", not a GUID)
    string? AssigneeUserId = null,
    string? AssigneeCompanyId = null,

    // Request metadata
    string? Channel = null,
    string? BankingSegment = null,
    bool? IsPma = null,

    // Geographic
    string? Province = null,
    string? District = null,

    // Date ranges
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    DateTime? SlaDueDateFrom = null,
    DateTime? SlaDueDateTo = null,
    DateTime? AssignedDateFrom = null,
    DateTime? AssignedDateTo = null,
    DateTime? AppointmentDateFrom = null,
    DateTime? AppointmentDateTo = null,

    // Sorting
    string? SortBy = null,
    string? SortDir = null
)
{
    // Additive fields set via init setters. Purpose and PropertyType are bound by the list and
    // export endpoints; the rest remain picker-only.
    public string? CustomerName { get; init; }
    public string? AppraisalNumber { get; init; }
    public string? Purpose { get; init; }

    // Matches appraisals having at least one property of the given type(s); comma-separated for IN.
    public string? PropertyType { get; init; }
    public string? SubDistrict { get; init; }
    public DateTime? RequestedAtFrom { get; init; }
    public DateTime? RequestedAtTo { get; init; }
}
