using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

/// <summary>
/// Result of getting all Appraisals
/// </summary>
public record GetAppraisalsResult(PaginatedResult<AppraisalDto> Result, AppraisalFacets? Facets = null);

/// <summary>
/// DTO for Appraisal list item
/// </summary>
public record AppraisalDto
{
    // Core Fields
    public Guid Id { get; init; }
    public string? AppraisalNumber { get; init; }
    public Guid RequestId { get; init; }
    public string? RequestNumber { get; init; }
    public string Status { get; init; } = null!;
    public string AppraisalType { get; init; } = null!;
    public string Priority { get; init; } = null!;
    public bool IsPma { get; init; }
    public string? Purpose { get; init; }
    public string? Channel { get; init; }
    public string? BankingSegment { get; init; }
    public decimal? FacilityLimit { get; init; }
    public string? RequestedBy { get; init; }
    public DateTime? RequestedAt { get; init; }
    public int? SLAHours { get; init; }
    public DateTime? SLADueDate { get; init; }
    public string? SLAStatus { get; init; }
    public int PropertyCount { get; init; }

    /// <summary>Distinct property type codes on this appraisal, comma-joined (e.g. "B, L, LB").</summary>
    public string? PropertyTypes { get; init; }
    public DateTime? CreatedAt { get; init; }
    public decimal? AppraisalValue { get; init; }

    // Assignment Info (from latest active assignment — stores username like "P5229")
    public string? AssigneeUserId { get; init; }
    public string? AssigneeCompanyId { get; init; }
    public string? AssignmentType { get; init; }
    public string? AssignmentStatus { get; init; }
    public DateTime? AssignedDate { get; init; }
    public string? CompanyName { get; init; }
    /// <summary>Thai name; null when the company has none. The client picks by its own locale.</summary>
    public string? CompanyNameLocal { get; init; }

    // Customer Info
    public string? CustomerName { get; init; }

    // Location Info (from first property's land detail)
    public string? Province { get; init; }
    public string? District { get; init; }
    public string? SubDistrict { get; init; }

    // Appointment
    public DateTime? AppointmentDateTime { get; init; }

    // SLA Computed
    public int? ElapsedHours { get; init; }
    public int? RemainingHours { get; init; }
}

/// <summary>
/// Aggregated facet counts for filter UI.
///
/// <b>Only <see cref="Status"/> is populated.</b> The other four are kept so the response shape does
/// not change, but they are always empty — an empty list here means "not computed", not "no matching
/// rows". Counting them is not free: AssignmentType alone has to resolve the latest assignment for
/// every matching appraisal, which costs more than the rest of the request combined, and no client
/// reads any of the four. If one is ever needed, add it as an opt-in dimension
/// (<c>?groupBy=</c>, the way <c>/tasks/me/group-counts</c> does it) rather than computing all five
/// on every list request.
/// </summary>
public record AppraisalFacets
{
    /// <summary>Counts per status, computed with the status filter itself excluded so the chips stay switchable.</summary>
    public List<FacetItem> Status { get; init; } = [];

    /// <summary>Always empty — see the remarks on <see cref="AppraisalFacets"/>.</summary>
    public List<FacetItem> SlaStatus { get; init; } = [];

    /// <summary>Always empty — see the remarks on <see cref="AppraisalFacets"/>.</summary>
    public List<FacetItem> Priority { get; init; } = [];

    /// <summary>Always empty — see the remarks on <see cref="AppraisalFacets"/>.</summary>
    public List<FacetItem> AppraisalType { get; init; } = [];

    /// <summary>Always empty — see the remarks on <see cref="AppraisalFacets"/>.</summary>
    public List<FacetItem> AssignmentType { get; init; } = [];
}

public record FacetItem(string Value, int Count);
