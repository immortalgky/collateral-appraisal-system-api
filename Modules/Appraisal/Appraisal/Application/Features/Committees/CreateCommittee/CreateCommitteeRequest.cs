namespace Appraisal.Application.Features.Committees.CreateCommittee;

public record CreateCommitteeRequest(
    string CommitteeName,
    string CommitteeCode,
    string QuorumType,
    int QuorumValue,
    string MajorityType,
    string? Description = null
);