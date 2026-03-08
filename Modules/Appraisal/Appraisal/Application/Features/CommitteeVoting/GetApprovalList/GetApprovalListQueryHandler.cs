namespace Appraisal.Application.Features.CommitteeVoting.GetApprovalList;

public class GetApprovalListQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetApprovalListQuery, GetApprovalListResult>
{
    public async Task<GetApprovalListResult> Handle(
        GetApprovalListQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.CommitteeName,
                ar.Status AS ReviewStatus,
                ar.Id AS ReviewId,
                cm.Id AS CommitteeMemberId,
                cm.MemberName,
                cm.Role,
                cv.Vote,
                CASE
                    WHEN cv.Vote IS NULL THEN 'Pending'
                    WHEN cv.Vote = 'Approve' THEN 'Agree'
                    WHEN cv.Vote = 'Reject' THEN 'Disagree'
                    WHEN cv.Vote = 'RouteBack' THEN 'Route Back'
                    ELSE cv.Vote
                END AS VoteLabel,
                cv.Comments AS Remark,
                cv.VotedAt
            FROM appraisal.AppraisalReviews ar
            JOIN appraisal.Committees c ON c.Id = ar.CommitteeId
            JOIN appraisal.CommitteeMembers cm ON cm.CommitteeId = c.Id AND cm.IsActive = 1
            LEFT JOIN appraisal.CommitteeVotes cv ON cv.ReviewId = ar.Id AND cv.CommitteeMemberId = cm.Id
            WHERE ar.AppraisalId = @AppraisalId
              AND ar.ReviewLevel = 'Committee'
            ORDER BY cm.MemberName
            """;

        var rows = (await connectionFactory.QueryAsync<ApprovalListRow>(sql, new { query.AppraisalId })).ToList();

        if (rows.Count == 0)
        {
            return new GetApprovalListResult(null, null, null, []);
        }

        var first = rows[0];
        var items = rows.Select(r => new ApprovalListItem(
            r.CommitteeMemberId,
            r.MemberName,
            r.Role,
            r.Vote,
            r.VoteLabel,
            r.Remark,
            r.VotedAt
        )).ToList();

        return new GetApprovalListResult(
            first.CommitteeName,
            first.ReviewStatus,
            first.ReviewId,
            items);
    }

    private record ApprovalListRow(
        string CommitteeName,
        string ReviewStatus,
        Guid ReviewId,
        Guid CommitteeMemberId,
        string MemberName,
        string Role,
        string? Vote,
        string VoteLabel,
        string? Remark,
        DateTime? VotedAt
    );
}
