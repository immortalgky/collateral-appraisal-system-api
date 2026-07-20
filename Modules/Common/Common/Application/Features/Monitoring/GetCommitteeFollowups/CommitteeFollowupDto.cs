namespace Common.Application.Features.Monitoring.GetMeetingFollowups;

public record CommitteeFollowupItemDto(
    Guid AppraisalId,
    string AppraisalNumber,
    string? CustomerName,
    string? MeetingNumber,
    DateTime? MeetingDate
);

public record CommitteeFollowupDto(
    Guid UserId,
    string UserName,
    string MemberName,
    int AvailableTasks,
    IReadOnlyList<CommitteeFollowupItemDto> Items
);