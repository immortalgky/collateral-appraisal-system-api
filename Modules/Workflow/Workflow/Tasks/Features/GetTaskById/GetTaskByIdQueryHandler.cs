using Dapper;
using Shared.Data;
using Shared.Exceptions;
using Shared.Identity;
using Workflow.AssigneeSelection.Teams;
using Workflow.Services.Groups;
using Workflow.Tasks.Authorization;

namespace Workflow.Tasks.Features.GetTaskById;

public class GetTaskByIdQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService,
    IUserGroupService userGroupService,
    ITeamService teamService
) : IQueryHandler<GetTaskByIdQuery, TaskDetailResult>
{
    private const string Sql = """
        SELECT
            pt.Id                              AS TaskId,
            pt.AssignedTo                      AS AssigneeUserId,
            pt.AssignedType,
            pt.AssigneeCompanyId,
            CAST(pt.TaskName AS nvarchar(100)) AS TaskName,
            pt.TaskDescription,
            pt.WorkflowInstanceId,
            pt.ActivityId,
            pt.WorkingBy,
            pt.LockedAt,
            -- Quotation context: pt.CorrelationId = QuotationRequestId for quotation-workflow tasks
            qr.Id                              AS QuotationRequestId,
            -- Resolve the effective RequestId:
            --   1. document-followup tasks: df.RequestId
            --   2. quotation-workflow tasks: qr.RequestId (the originating appraisal request)
            --   3. regular appraisal-workflow tasks: pt.CorrelationId (= requestId directly)
            COALESCE(df.RequestId, qr.RequestId, pt.CorrelationId) AS RequestId,
            -- Resolve the effective AppraisalId:
            --   1. document-followup tasks: df.AppraisalId
            --   2. quotation-workflow tasks: first linked appraisal via QuotationRequestAppraisals
            --   3. regular appraisal-workflow tasks: appraisal whose RequestId = pt.CorrelationId
            COALESCE(
                df.AppraisalId,
                qra_first.AppraisalId,
                (SELECT TOP 1 Id FROM appraisal.Appraisals
                 WHERE RequestId = pt.CorrelationId
                 ORDER BY CreatedAt DESC)
            )                                  AS AppraisalId
        FROM workflow.PendingTasks pt
        -- Followup tasks: resolve RequestId / AppraisalId via FollowupWorkflowInstanceId
        OUTER APPLY (SELECT TOP 1 RequestId, AppraisalId
                     FROM workflow.DocumentFollowups
                     WHERE FollowupWorkflowInstanceId = pt.WorkflowInstanceId) df
        -- Quotation tasks: pt.CorrelationId = QuotationRequestId for quotation-workflow instances
        OUTER APPLY (SELECT TOP 1 qr2.Id, qr2.RequestId
                     FROM appraisal.QuotationRequests qr2
                     WHERE qr2.Id = pt.CorrelationId) qr
        -- First appraisal linked to the quotation (ordered by join-table AddedAt for determinism)
        OUTER APPLY (SELECT TOP 1 qra2.AppraisalId
                     FROM appraisal.QuotationRequestAppraisals qra2
                     WHERE qra2.QuotationRequestId = qr.Id
                     ORDER BY qra2.AddedAt) qra_first
        WHERE pt.Id = @TaskId
        """;

    public async Task<TaskDetailResult> Handle(
        GetTaskByIdQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();

        var dto = await connection.QuerySingleOrDefaultAsync<TaskDetailDto>(
            Sql,
            new { query.TaskId });

        if (dto is null)
            throw new NotFoundException(nameof(TaskDetailResult), query.TaskId);

        var username = currentUserService.Username;

        bool isOwner;
        if (dto.AssignedType == "2")
        {
            if (string.IsNullOrEmpty(username))
            {
                isOwner = false;
            }
            else
            {
                var groups = await userGroupService.GetGroupsForUserAsync(username, cancellationToken);
                var team   = await teamService.GetTeamForUserAsync(username, cancellationToken);
                isOwner = PoolTaskAccess.IsOwner(
                    dto.AssigneeUserId,
                    dto.AssigneeCompanyId,
                    groups,
                    team?.TeamId,
                    currentUserService.CompanyId);
            }
        }
        else
        {
            isOwner = string.Equals(dto.AssigneeUserId, username,
                StringComparison.OrdinalIgnoreCase);
        }

        return new TaskDetailResult
        {
            TaskId = dto.TaskId,
            RequestId = dto.RequestId,
            AppraisalId = dto.AppraisalId,
            QuotationRequestId = dto.QuotationRequestId,
            WorkflowInstanceId = dto.WorkflowInstanceId,
            ActivityId = dto.ActivityId,
            AssigneeUserId = dto.AssigneeUserId,
            AssignedType = dto.AssignedType,
            TaskName = dto.TaskName,
            TaskDescription = dto.TaskDescription,
            IsOwner = isOwner,
            WorkingBy = dto.WorkingBy,
            LockedAt = dto.LockedAt
        };
    }

    // Constructor parameter order MUST match the SELECT column order above —
    // Dapper uses positional binding for records.
    private sealed record TaskDetailDto(
        Guid TaskId,
        string AssigneeUserId,
        string AssignedType,
        Guid? AssigneeCompanyId,
        string? TaskName,
        string? TaskDescription,
        Guid WorkflowInstanceId,
        string ActivityId,
        string? WorkingBy,
        DateTime? LockedAt,
        Guid? QuotationRequestId,
        Guid RequestId,
        Guid? AppraisalId);
}
