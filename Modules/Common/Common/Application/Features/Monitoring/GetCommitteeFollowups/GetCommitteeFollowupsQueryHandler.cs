using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetMeetingFollowups;

public class GetCommitteeFollowupsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetCommitteeFollowupsQuery, PaginatedResult<CommitteeFollowupDto>>
{
    public async Task<PaginatedResult<CommitteeFollowupDto>> Handle(
        GetCommitteeFollowupsQuery query,
        CancellationToken cancellationToken)
    {
        var filter = query.Filter;
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add(@"(MemberName LIKE @Search ESCAPE '\'
                OR EXISTS (SELECT 1 FROM common.vw_MonitoringCommitteeApprovalTasks s
                           WHERE s.AssignedTo = UserName
                             AND (s.AppraisalNumber LIKE @Search ESCAPE '\'
                                  OR s.CustomerName LIKE @Search ESCAPE '\')))");
            parameters.Add("Search", "%" + EscapeLike(filter.Search.Trim()) + "%");
        }
        if (!string.IsNullOrWhiteSpace(filter.MeetingNumber))
        {
            conditions.Add(@"EXISTS (SELECT 1 FROM common.vw_MonitoringCommitteeApprovalTasks s2
                                     WHERE s2.AssignedTo = UserName
                                       AND s2.MeetingNumber LIKE @MeetingNumber ESCAPE '\')");
            parameters.Add("MeetingNumber", "%" + EscapeLike(filter.MeetingNumber.Trim()) + "%");
        }
        if (filter.Tier is { Length: > 0 })
        {
            conditions.Add(@"EXISTS (SELECT 1 FROM common.vw_MonitoringCommitteeApprovalTasks s3
                                    WHERE s3.AssignedTo = UserName
                                    AND s3.ApprovalTier IN @Tiers)");
            parameters.Add("Tiers", filter.Tier);
        }

        if (filter.MeetingDateFrom is { } from)
        {
            conditions.Add(@"EXISTS (SELECT 1 FROM common.vw_MonitoringCommitteeApprovalTasks s4
                                    WHERE s4.AssignedTo = UserName
                                    AND s4.MeetingDate >= @MeetingDateFrom)");
            parameters.Add("MeetingDateFrom", from.ToDateTime(TimeOnly.MinValue));
        }

        if (filter.MeetingDateTo is { } to)
        {
            conditions.Add(@"EXISTS (SELECT 1 FROM common.vw_MonitoringCommitteeApprovalTasks s5
                                    WHERE s5.AssignedTo = UserName
                                    AND s5.MeetingDate <= @MeetingDateTo)");
            parameters.Add("MeetingDateTo", to.ToDateTime(TimeOnly.MaxValue));
        }

        var membersSql = $@"
            SELECT * FROM (
                SELECT
                    u.Id       AS UserId,
                    u.UserName AS UserName,
                    MAX(cm.MemberName)         AS MemberName,
                    COUNT(DISTINCT t.AppraisalId) AS AvailableTasks
                FROM common.vw_MonitoringCommitteeApprovalTasks t
                INNER JOIN auth.AspNetUsers u ON u.UserName = t.AssignedTo
                LEFT JOIN workflow.CommitteeMembers cm ON cm.UserId = u.UserName AND cm.IsActive = 1
                GROUP BY u.Id, u.UserName
            ) p";

        if (conditions.Count > 0)
            membersSql += " WHERE " + string.Join(" AND ", conditions);
        var sortDir = string.Equals(filter.SortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var orderBy = string.Equals(filter.SortBy, "AvailableTasks", StringComparison.OrdinalIgnoreCase)
            ? $"AvailableTasks {sortDir}, MemberName ASC"
            : $"MemberName {sortDir}";

        var page = await connectionFactory.QueryPaginatedAsync<CommitteeRow>(
            membersSql, orderBy, query.Paging, parameters);

        var usernames = page.Items.Select(x => x.UserName).ToArray();
        if (usernames.Length == 0)
            return new PaginatedResult<CommitteeFollowupDto>([], page.Count, page.PageNumber, page.PageSize);

        const string itemsSql = @"
        SELECT DISTINCT AssignedTo, AppraisalId, AppraisalNumber, CustomerName,
                MeetingNumber, MeetingDate
                FROM common.vw_MonitoringCommitteeApprovalTasks
                WHERE AssignedTo IN @UserNames
                ORDER BY AssignedTo, AppraisalNumber";

        var conn = connectionFactory.GetOpenConnection();
        var itemRows = (await conn.QueryAsync<ItemRow>(itemsSql, new { UserNames = usernames })).ToList();

        var lookup = itemRows
            .GroupBy(r => r.AssignedTo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<CommitteeFollowupItemDto>)g
                    .Select(r => new CommitteeFollowupItemDto(
                        r.AppraisalId, r.AppraisalNumber, r.CustomerName,
                        r.MeetingNumber, r.MeetingDate))
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var items = page.Items
            .Select(m => new CommitteeFollowupDto(
                m.UserId, m.UserName, m.MemberName ?? m.UserName, m.AvailableTasks,
                lookup.TryGetValue(m.UserName, out var list) ? list : []))
            .ToList();

        return new PaginatedResult<CommitteeFollowupDto>(items, page.Count, page.PageNumber, page.PageSize);
    }

    private static string EscapeLike(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    private record CommitteeRow(Guid UserId, string UserName, string? MemberName, int AvailableTasks);
    private record ItemRow(string AssignedTo, Guid AppraisalId, string AppraisalNumber, string? CustomerName,
        string? MeetingNumber, DateTime? MeetingDate);
}