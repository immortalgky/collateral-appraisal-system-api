using Shared.CQRS;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetMeetingFollowups;

public record GetMeetingFollowupsQuery(
    PaginationRequest Paging,
    MeetingFollowupFilter Filter
) : IQuery<PaginatedResult<MeetingFollowupDto>>;
