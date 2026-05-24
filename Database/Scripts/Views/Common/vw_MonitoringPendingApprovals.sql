CREATE
OR ALTER VIEW common.vw_MonitoringPendingApprovals
AS
-- Pending committee approvals across all 3 tiers — one row per appraisal.
-- An appraisal appears here when at least one of its current-round approvers has not yet voted
-- (i.e. SUM(IsPending) > 0 after grouping by AppraisalId).
--
-- Primary data source: workflow.vw_AppraisalApprovalStatus
--   * Its 'roster' CTE already isolates the current round (latest_exec). No further
--     round-scoping is needed here.
--   * Tiers: 1 = SUB_COMMITTEE (≤9.99M, no meeting), 2 = COMMITTEE (≤30M, no meeting),
--            3 = COMMITTEE_WITH_MEETING (>30M; approval tasks only exist after the secretary
--            releases the meeting item into 'pending-approval').
--
-- FAN-OUT GUARD: the source view joins appraisal via RequestId (CorrelationId of the workflow
-- instance). For requests with multiple appraisals (CI / Appeal flows), that join can attach
-- a single workflow instance's approval roster to ALL appraisals on the same request and
-- produce phantom rows. We constrain the appraisal binding by extracting 'appraisalId' from
-- workflow.WorkflowInstances.Variables (JSON) and matching it to a.AppraisalId. Workflows
-- without an appraisalId variable (legacy / non-appraisal flows) are excluded by the inner join.
--
-- Tier 3 meeting context is sourced from workflow.MeetingItems (ItemDecision = 'Released',
-- Kind = 'Decision'), NOT from appraisal.AppraisalReviews which records the post-final-approval
-- outcome. Using MeetingItems ensures we see the in-flight meeting, not a prior completed review.
-- The Kind filter is defensive: today only Decision items reach 'Released', but explicit Kind
-- gating avoids a silent regression if Acknowledgement items ever start using that state.
--
-- CustomerName: same OUTER APPLY TOP 1 pattern used in vw_MonitoringPendingTasks.
-- WorstSlaStatus: derived from per-row SlaStatus by mapping strings to severity ints
--   (Breached=2, AtRisk=1, OnTime=0) and taking the MAX per group. Voted rows (IsPending=0)
--   are excluded from the aggregate by the outer CASE returning NULL for non-pending.
SELECT
    a.AppraisalId,
    a.AppraisalNumber,
    -- Customer: first customer of the linked request
    OUTER_C.Name                                                                   AS CustomerName,
    a.ApprovalTier,
    -- Pending / total approvers for the current round
    SUM(CASE WHEN a.IsPending = 1 THEN 1 ELSE 0 END)                              AS PendingCount,
    COUNT(*)                                                                       AS TotalApprovers,
    -- Earliest due date among still-pending approvers
    MIN(CASE WHEN a.IsPending = 1 THEN a.DueAt END)                               AS EarliestDueAt,
    -- Worst SLA status among still-pending approvers:
    --   2 = Breached  →  'Breached'
    --   1 = AtRisk    →  'AtRisk'
    --   0 = OnTime    →  'OnTime'
    -- NULL DueAt / NULL SlaStatus for voted members is ignored by the outer CASE.
    CASE MAX(
        CASE WHEN a.IsPending = 1
             THEN CASE a.SlaStatus
                      WHEN 'Breached' THEN 2
                      WHEN 'AtRisk'   THEN 1
                      ELSE 0
                  END
        END)
        WHEN 2 THEN 'Breached'
        WHEN 1 THEN 'AtRisk'
        ELSE 'OnTime'
    END                                                                            AS WorstSlaStatus,
    -- Tier 3 meeting context: most-recently released meeting item for this appraisal.
    -- ItemDecision = 'Released' means the secretary released this item after the meeting
    -- ended, triggering the pending-approval workflow activity.
    mi_ctx.MeetingId,
    m_ctx.MeetingNo                                                                AS MeetingNumber,
    m_ctx.StartAt                                                                  AS MeetingDate,
    m_ctx.Status                                                                   AS MeetingStatus
FROM workflow.vw_AppraisalApprovalStatus a
    -- Resolve the Appraisal row for RequestId-based customer lookup
    JOIN appraisal.Appraisals appl
        ON appl.Id = a.AppraisalId
    -- Fan-out guard: validate the appraisal binding via the workflow instance's appraisalId variable
    JOIN workflow.WorkflowInstances wi
        ON wi.Id = a.WorkflowInstanceId
       AND a.AppraisalId = TRY_CAST(JSON_VALUE(wi.Variables, '$.appraisalId') AS UNIQUEIDENTIFIER)
    -- Customer name: first customer of the appraisal's originating request
    OUTER APPLY (
        SELECT TOP 1 rc.Name
        FROM request.RequestCustomers rc
        WHERE rc.RequestId = appl.RequestId
    ) OUTER_C
    -- Tier 3 meeting context: the most-recently released Decision MeetingItem for this appraisal.
    -- OUTER APPLY returns NULL for tiers 1 and 2 (no MeetingItems with ItemDecision='Released').
    OUTER APPLY (
        SELECT TOP 1 mi2.MeetingId
        FROM workflow.MeetingItems mi2
        WHERE mi2.AppraisalId = a.AppraisalId
          AND mi2.ItemDecision = 'Released'
          AND mi2.Kind = 'Decision'
        ORDER BY mi2.DecisionAt DESC, mi2.Id DESC
    ) mi_ctx
    LEFT JOIN workflow.Meetings m_ctx
        ON m_ctx.Id = mi_ctx.MeetingId
GROUP BY
    a.AppraisalId,
    a.AppraisalNumber,
    OUTER_C.Name,
    a.ApprovalTier,
    mi_ctx.MeetingId,
    m_ctx.MeetingNo,
    m_ctx.StartAt,
    m_ctx.Status
HAVING SUM(CASE WHEN a.IsPending = 1 THEN 1 ELSE 0 END) > 0;
