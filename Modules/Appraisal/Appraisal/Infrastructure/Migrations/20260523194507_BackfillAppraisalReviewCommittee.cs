using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <summary>
    /// One-time backfill: create the committee-approval outcome row in appraisal.AppraisalReviews
    /// for every appraisal approved before the write-path existed. Idempotent (NOT EXISTS guard).
    /// Runs after the Workflow migrations (EF migrates WorkflowDbContext before AppraisalDbContext),
    /// so the cross-schema workflow.* reads are safe.
    /// </summary>
    public partial class BackfillAppraisalReviewCommittee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH approved AS (
                    SELECT a.Id            AS AppraisalId,
                           a.RequestId,
                           a.CompletedAt,
                           a.ApprovedByCommittee AS CommitteeCode
                    FROM appraisal.Appraisals a
                    WHERE a.CompletedAt IS NOT NULL
                      AND a.ApprovedByCommittee IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM appraisal.AppraisalReviews ar
                                      WHERE ar.AppraisalId = a.Id)
                ),
                -- Latest pending-approval execution per appraisal (correlate votes -> instance -> request).
                votes_exec AS (
                    SELECT ap.AppraisalId,
                           av.ActivityExecutionId,
                           ROW_NUMBER() OVER (PARTITION BY ap.AppraisalId
                                              ORDER BY MAX(av.VotedAt) DESC, av.ActivityExecutionId DESC) AS rn
                    FROM approved ap
                    JOIN workflow.WorkflowInstances wi
                         ON wi.CorrelationId = CONVERT(NVARCHAR(36), ap.RequestId)
                    JOIN workflow.ApprovalVotes av
                         ON av.WorkflowInstanceId = wi.Id AND av.ActivityId = 'pending-approval'
                    GROUP BY ap.AppraisalId, av.ActivityExecutionId
                ),
                votes AS (
                    SELECT ve.AppraisalId,
                           SUM(CASE WHEN LOWER(av.Vote) = 'approve'    THEN 1 ELSE 0 END) AS VotesApprove,
                           SUM(CASE WHEN LOWER(av.Vote) = 'reject'     THEN 1 ELSE 0 END) AS VotesReject,
                           SUM(CASE WHEN LOWER(av.Vote) = 'route_back' THEN 1 ELSE 0 END) AS VotesRouteBack,
                           COUNT(*) AS TotalVotes
                    FROM votes_exec ve
                    JOIN workflow.ApprovalVotes av ON av.ActivityExecutionId = ve.ActivityExecutionId
                    WHERE ve.rn = 1
                    GROUP BY ve.AppraisalId
                ),
                -- Decision meeting (committee with meeting): the meeting that carried this appraisal as a Decision item.
                decision_mtg AS (
                    SELECT mi.AppraisalId, mi.MeetingId,
                           ROW_NUMBER() OVER (PARTITION BY mi.AppraisalId ORDER BY mi.AddedAt DESC) AS rn
                    FROM workflow.MeetingItems mi
                    WHERE mi.Kind = 'Decision'
                ),
                -- Acknowledgement meeting (sub-committee / committee): where the approval was acknowledged.
                ack_mtg AS (
                    SELECT q.AppraisalId, q.MeetingId,
                           ROW_NUMBER() OVER (PARTITION BY q.AppraisalId ORDER BY q.EnqueuedAt DESC) AS rn
                    FROM workflow.AppraisalAcknowledgementQueueItems q
                    WHERE q.Status = 'Acknowledged' AND q.MeetingId IS NOT NULL
                )
                INSERT INTO appraisal.AppraisalReviews
                    (AppraisalId, CommitteeId, TotalVotes, VotesApprove, VotesReject, VotesAbstain,
                     MeetingId, ReviewedAt, CreatedAt, CreatedBy)
                SELECT ap.AppraisalId,
                       c.Id,
                       v.TotalVotes,
                       v.VotesApprove,
                       v.VotesReject,
                       v.VotesRouteBack,                       -- route-back occupies the spare abstain tally
                       COALESCE(dm.MeetingId, am.MeetingId),
                       ap.CompletedAt,
                       GETDATE(),
                       'system'
                FROM approved ap
                LEFT JOIN appraisal.Committees c ON c.CommitteeCode = ap.CommitteeCode
                LEFT JOIN votes v ON v.AppraisalId = ap.AppraisalId
                LEFT JOIN decision_mtg dm ON dm.AppraisalId = ap.AppraisalId AND dm.rn = 1
                LEFT JOIN ack_mtg am ON am.AppraisalId = ap.AppraisalId AND am.rn = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-time data backfill; not reversed.
        }
    }
}
