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
    public Guid Id { get; set; }
    public string? AppraisalNumber { get; set; }
    public Guid RequestId { get; set; }
    public string Status { get; set; } = null!;
    public string AppraisalType { get; set; } = null!;
    public string Priority { get; set; } = null!;
    public int? SLADays { get; set; }
    public DateTime? SLADueDate { get; set; }
    public string? SLAStatus { get; set; }
    public int PropertyCount { get; set; }
    public DateTime? CreatedOn { get; set; }
}