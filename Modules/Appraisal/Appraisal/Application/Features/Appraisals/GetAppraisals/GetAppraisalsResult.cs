using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

/// <summary>
/// Result of getting all Appraisals
/// </summary>
public record GetAppraisalsResult(PaginatedResult<AppraisalDto> Result);

/// <summary>
/// DTO for Appraisal list item
/// </summary>
public record AppraisalDto
{
    // Core Fields
    public Guid Id { get; init; }
    public string? AppraisalNumber { get; init; }
    public Guid RequestId { get; init; }
    public string Status { get; init; } = null!;
    public string AppraisalType { get; init; } = null!;
    public string Priority { get; init; } = null!;
    public bool IsPma { get; init; }
    public string? Channel { get; init; }
    public string? BankingSegment { get; init; }
    public decimal? FacilityLimit { get; init; }
    public int? SLADays { get; init; }
    public DateTime? SLADueDate { get; init; }
    public string? SLAStatus { get; init; }
    public int PropertyCount { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? AppointmentDateTime { get; init; }

    // Assignment Info (from latest assignment)
    public Guid? AssigneeUserId { get; init; }
    public string? AssignmentStatus { get; init; }
    public DateTime? AssignedDate { get; init; }

    // Location Info (from first property's land detail)
    public string? Province { get; init; }
}