using Common.Application.Features.Monitoring.Shared;
using Shared.CQRS;

namespace Common.Application.Features.Monitoring.GetMeetingFollowups;

public record GetMeetingFollowupsSummaryQuery(
    MeetingFollowupFilter Filter
) : IQuery<MonitoringSummaryDto>;
