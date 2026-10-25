using Dapper;

namespace Appraisal.Application.Features.Quotations.AutoAssign;

internal sealed class QuotationAutoAssignStateReader(ISqlConnectionFactory connectionFactory)
    : IQuotationAutoAssignStateReader
{
    // WorkflowInstances.CorrelationId is the request id as a lower-case string. PendingTasks are
    // scoped by instance (not CorrelationId) because child workflows share the request id.
    // Cross-module read of workflow tables follows GetAppraisalBriefQueryHandler; no workflow JSON is read.
    // Name of the main appraisal workflow definition; defined by the Workflow module (no shared constant).
    private const string MainWorkflowName = "Collateral Appraisal Workflow";

    private const string StateSql = """
        SELECT a.Id AS AppraisalId,
               a.AppraisalNumber,
               a.IsPma,
               a.AppraisalType,
               wi.Id AS WorkflowInstanceId,
               wi.Status AS WorkflowStatus,
               pt.Id AS PendingTaskId,
               pt.ActivityId AS PendingActivityId,
               pt.WorkingBy,
               pt.HasOpenFollowup,
               pt.AssignedTo AS TaskAssignedTo,
               pt.AssignedType AS TaskAssignedType,
               asg.AssignmentStatus,
               asg.InternalAppraiserId AS FollowupStaffId,
               NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(u.FirstName, N''), N' ', NULLIF(u.LastName, N'')))), N'')
                   AS FollowupStaffName,
               asg.InternalFollowupAssignmentMethod AS FollowupMethod,
               asg.HasAdminBaseFee
        FROM appraisal.Appraisals a
        -- One main-workflow instance per appraisal: a live one if any, else the newest.
        OUTER APPLY (SELECT TOP 1 w.Id, w.Status
                     FROM workflow.WorkflowInstances w
                     WHERE w.CorrelationId = LOWER(CONVERT(nvarchar(36), a.RequestId))
                       AND w.WorkflowDefinitionId IN (SELECT Id FROM workflow.WorkflowDefinitions WHERE Name = @WorkflowName)
                     ORDER BY CASE WHEN w.Status IN ('Cancelled', 'Completed', 'Failed') THEN 1 ELSE 0 END, w.StartedOn DESC) wi
        OUTER APPLY (SELECT TOP 1 t.Id, t.ActivityId, t.WorkingBy, t.AssignedTo, t.AssignedType,
                            -- RequireDocumentFollowupClearedStep gate: appraisal-assignment opts in (canRaiseFollowup).
                            CASE WHEN EXISTS (SELECT 1 FROM workflow.DocumentFollowups f
                                              WHERE f.RaisingPendingTaskId = t.Id AND f.Status = 'Open')
                                 THEN 1 ELSE 0 END AS HasOpenFollowup
                     FROM workflow.PendingTasks t
                     WHERE t.WorkflowInstanceId = wi.Id
                     ORDER BY t.AssignedAt DESC, t.Id DESC) pt
        OUTER APPLY (SELECT TOP 1 x.AssignmentStatus, x.InternalAppraiserId, x.InternalFollowupAssignmentMethod,
                            -- The admin's own base fee (above 0) outranks the quotation, so an unpriced winner is fine then.
                            CASE WHEN EXISTS (SELECT 1
                                              FROM appraisal.AppraisalFees f
                                              JOIN appraisal.AppraisalFeeItems i ON i.AppraisalFeeId = f.Id
                                              WHERE f.AssignmentId = x.Id
                                                AND i.FeeCode = @BaseFeeCode AND i.Source = @AdminSource
                                                AND i.FeeAmount > 0)
                                 THEN 1 ELSE 0 END AS HasAdminBaseFee
                     FROM appraisal.AppraisalAssignments x
                     WHERE x.AppraisalId = a.Id
                     -- A Pending row first: it is the one AssignAppraisalCommand will act on.
                     ORDER BY CASE WHEN x.AssignmentStatus = @PendingStatus THEN 0 ELSE 1 END,
                              x.CreatedAt DESC, x.Id DESC) asg
        LEFT JOIN auth.AspNetUsers u ON u.NormalizedUserName = UPPER(asg.InternalAppraiserId)
        WHERE a.Id IN @AppraisalIds AND a.IsDeleted = 0
        """;

    public async Task<IReadOnlyList<AppraisalAssignState>> GetAsync(
        IReadOnlyCollection<Guid> appraisalIds, CancellationToken cancellationToken)
    {
        if (appraisalIds.Count == 0)
            return [];

        var rows = await connectionFactory.GetOpenConnection().QueryAsync<AppraisalAssignState>(
            new CommandDefinition(
                StateSql,
                new
                {
                    AppraisalIds = appraisalIds,
                    WorkflowName = MainWorkflowName,
                    PendingStatus = AssignmentStatus.Pending.Code,
                    BaseFeeCode = AppraisalFee.BaseFeeCode,
                    AdminSource = FeeItemSource.Admin
                },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<Guid>?> GetQuotationAppraisalIdsAsync(
        Guid quotationId, CancellationToken cancellationToken)
    {
        var rows = (await connectionFactory.GetOpenConnection().QueryAsync<Guid?>(
            new CommandDefinition(
                """
                SELECT qra.AppraisalId
                FROM appraisal.QuotationRequests q
                LEFT JOIN appraisal.QuotationRequestAppraisals qra ON qra.QuotationRequestId = q.Id
                WHERE q.Id = @QuotationId
                """,
                new { QuotationId = quotationId },
                cancellationToken: cancellationToken))).ToList();
        return rows.Count == 0 ? null : rows.OfType<Guid>().ToList();
    }

    public async Task<bool> WorkflowInstanceBelongsToAppraisalAsync(
        Guid appraisalId, Guid workflowInstanceId, CancellationToken cancellationToken) =>
        await connectionFactory.GetOpenConnection().QuerySingleOrDefaultAsync<bool>(
            new CommandDefinition(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM appraisal.Appraisals a
                    JOIN workflow.WorkflowInstances w
                      ON w.CorrelationId = LOWER(CONVERT(nvarchar(36), a.RequestId))
                    WHERE a.Id = @AppraisalId
                      AND w.Id = @WorkflowInstanceId
                      AND w.WorkflowDefinitionId IN (SELECT Id FROM workflow.WorkflowDefinitions WHERE Name = @WorkflowName)
                ) THEN 1 ELSE 0 END
                """,
                new { AppraisalId = appraisalId, WorkflowInstanceId = workflowInstanceId, WorkflowName = MainWorkflowName },
                cancellationToken: cancellationToken));

    public async Task<bool> IsStandaloneAsync(Guid quotationId, CancellationToken cancellationToken) =>
        await connectionFactory.GetOpenConnection().QuerySingleOrDefaultAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN WorkflowInstanceId IS NULL THEN 1 ELSE 0 END FROM appraisal.QuotationRequests WHERE Id = @QuotationId",
                new { QuotationId = quotationId },
                cancellationToken: cancellationToken));

    public async Task<QuotationWinner> GetWinnerAsync(Guid quotationId, CancellationToken cancellationToken) =>
        await connectionFactory.GetOpenConnection().QuerySingleOrDefaultAsync<QuotationWinner>(
            new CommandDefinition(
                """
                SELECT cq.Id AS CompanyQuotationId, cq.CompanyId
                FROM appraisal.QuotationRequests q
                JOIN appraisal.CompanyQuotations cq ON cq.Id = COALESCE(q.SelectedQuotationId, q.TentativeWinnerQuotationId)
                WHERE q.Id = @QuotationId
                """,
                new { QuotationId = quotationId },
                cancellationToken: cancellationToken)) ?? new QuotationWinner(null, null);

    public async Task<IReadOnlyList<Guid>> GetPricedAppraisalIdsAsync(
        Guid companyQuotationId, CancellationToken cancellationToken) =>
        (await connectionFactory.GetOpenConnection().QueryAsync<Guid>(
            new CommandDefinition(
                """
                SELECT AppraisalId
                FROM (
                    SELECT AppraisalId,
                           ROW_NUMBER() OVER (PARTITION BY AppraisalId ORDER BY ItemNumber, Id) AS Rn,
                           CASE WHEN FeeAmount > 0 THEN FeeAmount - Discount - ISNULL(NegotiatedDiscount, 0)
                                ELSE QuotedPrice END AS Price
                    FROM appraisal.CompanyQuotationItems
                    WHERE CompanyQuotationId = @CompanyQuotationId
                ) items
                -- The fee consumer (QuotationFeePrice.Resolve) bills the FIRST item of an appraisal by ItemNumber, then Id.
                WHERE Rn = 1 AND Price > 0
                """,
                new { CompanyQuotationId = companyQuotationId },
                cancellationToken: cancellationToken))).ToList();
}
