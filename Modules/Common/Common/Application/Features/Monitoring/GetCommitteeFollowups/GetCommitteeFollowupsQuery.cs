using Shared.CQRS;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetMeetingFollowups;

public record GetCommitteeFollowupsQuery(
    PaginationRequest Paging,
    MeetingFollowupFilter Filter
) : IQuery<PaginatedResult<CommitteeFollowupDto>>;