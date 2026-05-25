namespace Common.Application.Features.Monitoring.GetMeetingFollowups;

public record MeetingFollowupFilter(
    string? Search,
    string? SortBy,
    string? SortDir,
    int[]? Tier,
    string[]? SlaStatus,
    string[]? SlaBucket,
    string? MeetingNumber,
    DateOnly? MeetingDateFrom,
    DateOnly? MeetingDateTo
);
