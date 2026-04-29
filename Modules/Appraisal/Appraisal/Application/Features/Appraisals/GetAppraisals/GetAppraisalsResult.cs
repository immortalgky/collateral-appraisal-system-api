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
    public int? SLADays { get; init; }
    public DateTime? SLADueDate { get; init; }
    public string? SLAStatus { get; init; }
    public int PropertyCount { get; init; }
    public DateTime? CreatedAt { get; init; }

    // Assignment Info (from latest active assignment — stores username like "P5229")
    public string? AssigneeUserId { get; init; }
    public string? AssigneeCompanyId { get; init; }
    public string? AssignmentType { get; init; }
    public string? AssignmentStatus { get; init; }
    public DateTime? AssignedDate { get; init; }
    public string? CompanyName { get; init; }

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
/// Aggregated facet counts for filter UI
/// </summary>
public record AppraisalFacets
{
    public List<FacetItem> Status { get; init; } = [];
    public List<FacetItem> SlaStatus { get; init; } = [];
    public List<FacetItem> Priority { get; init; } = [];
    public List<FacetItem> AppraisalType { get; init; } = [];
    public List<FacetItem> AssignmentType { get; init; } = [];
}

public record FacetItem(string Value, int Count);
