using Shared.Pagination;

namespace Appraisal.Application.Features.Committees.GetCommittees;

/// <summary>
/// Result of getting all Committees
/// </summary>
public record GetCommitteesResult(PaginatedResult<CommitteeDto> Result);

/// <summary>
/// DTO for Committee list item
/// </summary>
public record CommitteeDto
{
    public Guid Id { get; set; }
    public string CommitteeName { get; set; } = null!;
    public string CommitteeCode { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string QuorumType { get; set; } = null!;
    public int QuorumValue { get; set; }
    public string MajorityType { get; set; } = null!;
    public int MemberCount { get; set; }
    public int ConditionCount { get; set; }
    public DateTime? CreatedOn { get; set; }
}