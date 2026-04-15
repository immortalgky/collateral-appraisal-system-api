using System.Text.Json;
using Auth.Contracts.Users;
using Dapper;
using Microsoft.Extensions.Logging;
using Shared.CQRS;
using Shared.Data;

namespace Workflow.Workflow.Features.GetAppraisalWorkflowProgress;

public class GetAppraisalWorkflowProgressQueryHandler(
    ISqlConnectionFactory connectionFactory,
    IUserLookupService userLookupService,
    ILogger<GetAppraisalWorkflowProgressQueryHandler> logger
) : IQueryHandler<GetAppraisalWorkflowProgressQuery, GetAppraisalWorkflowProgressResponse>
{
    // ── Static group map ──────────────────────────────────────────────────────
    private static readonly Dictionary<string, string> GroupMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["appraisal-initiation"] = "Initiation",
        ["appraisal-initiation-check"] = "Initiation",
        ["appraisal-assignment"] = "Assignment",
        ["ext-appraisal-assignment"] = "Execution",
        ["ext-appraisal-execution"] = "Execution",
        ["ext-appraisal-check"] = "Execution",
        ["ext-appraisal-verification"] = "Execution",
        ["int-appraisal-execution"] = "Execution",
        ["appraisal-book-verification"] = "Staff",
        ["int-appraisal-check"] = "Checker",
        ["int-appraisal-verification"] = "Verification",
        ["pending-approval"] = "Approval"
    };

    // ── Fallback phase sequences ──────────────────────────────────────────────
    private static readonly Dictionary<string, List<string>> FallbackGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        ["External"] = ["Initiation", "Assignment", "Execution", "Staff", "Checker", "Verification", "Approval"],
        ["Internal"] = ["Initiation", "Assignment", "Execution", "Checker", "Verification", "Approval"],
        ["Unknown"] = ["Initiation", "Assignment", "Approval"]
    };


    // ── SQL ───────────────────────────────────────────────────────────────────
    // Pre-query: resolve RequestId from AppraisalId.
    // WorkflowInstances, PendingTasks, and CompletedTasks all use requestId as CorrelationId
    // (set when the workflow is started for a request — NOT the appraisalId).
    private const string RequestIdSql = """
                                        SELECT RequestId FROM appraisal.Appraisals WHERE Id = @AppraisalId
                                        """;

    // Wave 1: instance + activity log + assignment type in one round trip.
    // @CorrelationId = requestId as string (WorkflowInstances stores it as nvarchar)
    // @RequestId     = requestId as Guid   (PendingTasks/CompletedTasks store it as uniqueidentifier)
    // @AppraisalId   = appraisalId as Guid (AppraisalAssignments is keyed by AppraisalId)
    private const string Wave1Sql = """
                                    SELECT wi.Id, wi.WorkflowDefinitionId, wi.Status, wi.CurrentActivityId, wi.Variables
                                    FROM workflow.WorkflowInstances wi
                                    WHERE wi.CorrelationId = @CorrelationId;

                                    SELECT pt.TaskName, pt.TaskDescription, pt.AssignedTo, pt.AssignedType, pt.AssignedAt,
                                           CAST(NULL AS datetime2)      AS CompletedAt,
                                           CAST(NULL AS nvarchar(10))   AS ActionTaken,
                                           CAST(NULL AS nvarchar(1000)) AS Remark,
                                           'Pending'                    AS TaskStatus,
                                           pt.ActivityId
                                    FROM workflow.PendingTasks pt
                                    WHERE pt.CorrelationId = @RequestId

                                    UNION ALL

                                    SELECT ct.TaskName, ct.TaskDescription, ct.AssignedTo, ct.AssignedType, ct.AssignedAt,
                                           ct.CompletedAt, ct.ActionTaken, ct.Remark,
                                           'Completed' AS TaskStatus,
                                           ct.ActivityId
                                    FROM workflow.CompletedTasks ct
                                    WHERE ct.CorrelationId = @RequestId

                                    ORDER BY AssignedAt;

                                    SELECT TOP 1 AssignmentType
                                    FROM appraisal.AppraisalAssignments
                                    WHERE AppraisalId = @AppraisalId
                                    ORDER BY AssignedAt DESC;
                                    """;

    // Wave 2 (instance exists): completed activity IDs + workflow definition JSON.
    private const string Wave2Sql = """
                                    SELECT DISTINCT ActivityId
                                    FROM workflow.WorkflowActivityExecutions
                                    WHERE WorkflowInstanceId = @WorkflowInstanceId
                                      AND Status = 'Completed';

                                    SELECT JsonDefinition
                                    FROM workflow.WorkflowDefinitions
                                    WHERE Id = @DefinitionId;
                                    """;

    // Fallback when no instance: latest active appraisal workflow definition.
    private const string FallbackDefinitionSql = """
                                                 SELECT TOP 1 JsonDefinition
                                                 FROM workflow.WorkflowDefinitions
                                                 WHERE IsActive = 1
                                                   AND Category = 'Appraisal'
                                                 ORDER BY CreatedOn DESC
                                                 """;

    // ── Handler ───────────────────────────────────────────────────────────────
    public async Task<GetAppraisalWorkflowProgressResponse> Handle(
        GetAppraisalWorkflowProgressQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();
        var appraisalId = query.AppraisalId;

        // Resolve requestId — workflow tables use requestId as CorrelationId, not appraisalId
        var requestId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            RequestIdSql,
            new { AppraisalId = appraisalId });

        if (requestId is null)
            return new GetAppraisalWorkflowProgressResponse
                { RouteType = "Unknown", Steps = [], ActivityLog = [] };

        // Wave 1 — instance + activity log + assignment type in one round trip
        using var wave1 = await connection.QueryMultipleAsync(
            Wave1Sql,
            new
            {
                CorrelationId = requestId.Value.ToString(), // nvarchar in WorkflowInstances
                RequestId = requestId.Value, // uniqueidentifier in PendingTasks/CompletedTasks
                AppraisalId = appraisalId // AppraisalAssignments is keyed by AppraisalId
            });

        var instance = await wave1.ReadSingleOrDefaultAsync<WorkflowInstanceRow>();
        var logRows = (await wave1.ReadAsync<ActivityLogRow>()).ToList();
        var assignmentType = await wave1.ReadSingleOrDefaultAsync<string?>();

        // Wave 2 — completed activity IDs + definition JSON (conditional on instance)
        List<string> completedActivityIds = [];
        string? definitionJson = null;

        if (instance is not null)
        {
            using var wave2 = await connection.QueryMultipleAsync(
                Wave2Sql,
                new { WorkflowInstanceId = instance.Id, DefinitionId = instance.WorkflowDefinitionId });

            completedActivityIds = (await wave2.ReadAsync<string>()).ToList();
            definitionJson = await wave2.ReadSingleOrDefaultAsync<string?>();
        }
        else
        {
            definitionJson = await connection.QuerySingleOrDefaultAsync<string?>(FallbackDefinitionSql);
        }

        // ── Route detection ───────────────────────────────────────────────────
        var routeType = DetectRoute(completedActivityIds, instance?.CurrentActivityId, assignmentType);

        // ── Step building (BFS over workflow definition) ──────────────────────
        var orderedGroups = BuildOrderedGroups(definitionJson, routeType, logger);

        // ── Step statuses ─────────────────────────────────────────────────────
        var steps = BuildSteps(orderedGroups, completedActivityIds, instance);

        // ── Activity log with user display names ──────────────────────────────
        var activityLog = await BuildActivityLogAsync(logRows, cancellationToken);

        return new GetAppraisalWorkflowProgressResponse
        {
            WorkflowInstanceId = instance?.Id,
            WorkflowStatus = instance?.Status,
            RouteType = routeType,
            CurrentActivityId = instance?.CurrentActivityId,
            Steps = steps,
            ActivityLog = activityLog
        };
    }

    // ── Route detection ───────────────────────────────────────────────────────
    private static string DetectRoute(
        List<string> completedActivityIds,
        string? currentActivityId,
        string? assignmentType)
    {
        if (completedActivityIds.Any(id => id.StartsWith("ext-", StringComparison.OrdinalIgnoreCase)))
            return "External";
        if (completedActivityIds.Any(id => id.StartsWith("int-", StringComparison.OrdinalIgnoreCase)))
            return "Internal";

        if (currentActivityId is not null)
        {
            if (currentActivityId.StartsWith("ext-", StringComparison.OrdinalIgnoreCase)) return "External";
            if (currentActivityId.StartsWith("int-", StringComparison.OrdinalIgnoreCase)) return "Internal";
        }

        if (string.Equals(assignmentType, "External", StringComparison.OrdinalIgnoreCase)) return "External";
        if (string.Equals(assignmentType, "Internal", StringComparison.OrdinalIgnoreCase)) return "Internal";

        return "Unknown";
    }

    // ── BFS step building ─────────────────────────────────────────────────────
    private static List<string> BuildOrderedGroups(string? definitionJson, string routeType, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(definitionJson))
            return GetFallbackGroups(routeType);

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var def = JsonSerializer.Deserialize<WorkflowDefinitionDto>(definitionJson, options);
            var schema = def?.WorkflowSchema;

            if (schema?.Activities is null || schema.Transitions is null)
                return GetFallbackGroups(routeType);

            var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in schema.Transitions)
            {
                if (!adjacency.TryGetValue(t.From, out var list))
                    adjacency[t.From] = list = [];
                list.Add(t.To);
            }

            var startId = (schema.Activities.FirstOrDefault(a => a.IsStartActivity)
                           ?? schema.Activities.FirstOrDefault(a =>
                               string.Equals(a.Id, "start", StringComparison.OrdinalIgnoreCase)))?.Id;

            if (startId is null)
                return GetFallbackGroups(routeType);

            var activityTypeMap =
                schema.Activities.ToDictionary(a => a.Id, a => a.Type, StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();
            var orderedActivityIds = new List<string>();

            queue.Enqueue(startId);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current)) continue;

                if (activityTypeMap.TryGetValue(current, out var type)
                    && (type.Equals("TaskActivity", StringComparison.OrdinalIgnoreCase)
                        || type.Equals("ApprovalActivity", StringComparison.OrdinalIgnoreCase)))
                    orderedActivityIds.Add(current);

                if (adjacency.TryGetValue(current, out var neighbors))
                    foreach (var n in neighbors.Where(n => !visited.Contains(n)))
                        queue.Enqueue(n);
            }

            // Filter by route
            orderedActivityIds = routeType switch
            {
                "External" => orderedActivityIds
                    .Where(id => !id.StartsWith("int-", StringComparison.OrdinalIgnoreCase))
                    .ToList(),
                "Internal" => orderedActivityIds
                    .Where(id => !id.StartsWith("ext-", StringComparison.OrdinalIgnoreCase)
                                 && !id.Equals("appraisal-book-verification", StringComparison.OrdinalIgnoreCase))
                    .ToList(),
                _ => orderedActivityIds
                    .Where(id => !id.StartsWith("ext-", StringComparison.OrdinalIgnoreCase)
                                 && !id.StartsWith("int-", StringComparison.OrdinalIgnoreCase)
                                 && !id.Equals("appraisal-book-verification", StringComparison.OrdinalIgnoreCase))
                    .ToList()
            };

            // Map to groups, deduplicate consecutive same group
            var orderedGroups = new List<string>();
            string? lastGroup = null;
            foreach (var id in orderedActivityIds)
            {
                if (!GroupMap.TryGetValue(id, out var group)) continue;
                if (group != lastGroup)
                {
                    orderedGroups.Add(group);
                    lastGroup = group;
                }
            }

            return orderedGroups.Count > 0 ? orderedGroups : GetFallbackGroups(routeType);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse workflow definition JSON; falling back to static phase groups");
            return GetFallbackGroups(routeType);
        }
    }

    private static List<string> GetFallbackGroups(string routeType)
    {
        return FallbackGroups.TryGetValue(routeType, out var groups)
            ? new List<string>(groups)
            : new List<string>(FallbackGroups["Unknown"]);
    }

    // ── Step statuses ─────────────────────────────────────────────────────────
    private static List<PhaseStepDto> BuildSteps(
        List<string> orderedGroups,
        List<string> completedActivityIds,
        WorkflowInstanceRow? instance)
    {
        if (orderedGroups.Count == 0) return [];

        // Workflow fully completed → all Completed
        if (string.Equals(instance?.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            return orderedGroups.Select(g => new PhaseStepDto { Group = g, Status = "Completed" }).ToList();

        var completedGroups = completedActivityIds
            .Select(id => GroupMap.TryGetValue(id, out var g) ? g : null)
            .Where(g => g is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

        string? currentGroup = null;
        if (instance?.CurrentActivityId is not null
            && GroupMap.TryGetValue(instance.CurrentActivityId, out var cg))
            currentGroup = cg;

        currentGroup ??= orderedGroups.FirstOrDefault(g => !completedGroups.Contains(g));

        var currentIndex = currentGroup is not null ? orderedGroups.IndexOf(currentGroup) : -1;

        return orderedGroups.Select((group, idx) =>
        {
            var status = currentIndex >= 0
                ? idx < currentIndex ? "Completed" : idx == currentIndex ? "Current" : "Pending"
                : completedGroups.Contains(group)
                    ? "Completed"
                    : "Pending";

            return new PhaseStepDto { Group = group, Status = status };
        }).ToList();
    }

    // ── Activity log ──────────────────────────────────────────────────────────
    private async Task<List<ActivityLogItemDto>> BuildActivityLogAsync(
        List<ActivityLogRow> rows,
        CancellationToken cancellationToken)
    {
        const string userAssignedType = "1";

        var usernames = rows
            .Where(r => r.AssignedType == userAssignedType && r.AssignedTo is not null)
            .Select(r => r.AssignedTo!)
            .Distinct()
            .ToArray();

        var userMap = usernames.Length > 0
            ? await userLookupService.GetByUsernamesAsync(usernames, cancellationToken)
            : new Dictionary<string, UserLookupDto>();

        return rows.Select((r, idx) =>
        {
            string? displayName = null;
            string? companyName = null;
            if (r.AssignedType == userAssignedType && r.AssignedTo is not null)
            {
                if (userMap.TryGetValue(r.AssignedTo, out var user))
                {
                    displayName = $"{user.FirstName} {user.LastName}".Trim();
                    if (string.IsNullOrWhiteSpace(displayName)) displayName = r.AssignedTo;
                    companyName = user.CompanyName;
                }
                else
                {
                    displayName = r.AssignedTo;
                }
            }

            string? timeTaken = null;
            if (r.CompletedAt.HasValue)
            {
                var elapsed = r.CompletedAt.Value - r.AssignedAt;
                if (elapsed.TotalSeconds >= 0)
                    timeTaken = $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m";
            }

            var group = r.ActivityId is not null && GroupMap.TryGetValue(r.ActivityId, out var g) ? g : null;

            return new ActivityLogItemDto
            {
                SequenceNo = idx + 1,
                ActivityName = r.TaskName,
                TaskDescription = r.TaskDescription,
                AssignedTo = r.AssignedTo,
                AssignedToDisplayName = displayName,
                StartDate = r.AssignedAt,
                EndDate = r.CompletedAt,
                ActionTaken = r.ActionTaken,
                TimeTaken = timeTaken,
                Remark = r.Remark,
                Status = r.TaskStatus,
                Group = group,
                ActivityId = r.ActivityId,
                CompanyName = companyName
            };
        }).ToList();
    }

    // ── Private row types ─────────────────────────────────────────────────────
    private sealed class WorkflowInstanceRow
    {
        public Guid Id { get; set; }
        public Guid WorkflowDefinitionId { get; set; }
        public string Status { get; set; } = default!;
        public string? CurrentActivityId { get; set; }
        public string? Variables { get; set; }
    }

    private sealed class ActivityLogRow
    {
        public string TaskName { get; set; } = default!;
        public string? TaskDescription { get; set; }
        public string? AssignedTo { get; set; }
        public string? AssignedType { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ActionTaken { get; set; }
        public string? Remark { get; set; }
        public string TaskStatus { get; set; } = default!;
        public string? ActivityId { get; set; }
    }

    // ── JSON DTOs for workflow definition ─────────────────────────────────────
    private sealed class WorkflowDefinitionDto
    {
        public WorkflowSchemaDto? WorkflowSchema { get; set; }
    }

    private sealed class WorkflowSchemaDto
    {
        public List<ActivityDto>? Activities { get; set; }
        public List<TransitionDto>? Transitions { get; set; }
    }

    private sealed class ActivityDto
    {
        public string Id { get; set; } = default!;
        public string Type { get; set; } = default!;
        public bool IsStartActivity { get; set; }
    }

    private sealed class TransitionDto
    {
        public string From { get; set; } = default!;
        public string To { get; set; } = default!;
    }
}