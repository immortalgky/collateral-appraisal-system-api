using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Data.Seed;
using Workflow.Data;
using Workflow.Data.Entities;

namespace Workflow.Services.Configuration;

/// <summary>
/// Seeds example <see cref="TaskAssignmentConfiguration"/> override rows, and the pending-approval
/// conflicted-member rule. Idempotent: each part is skipped if its rows already exist.
///
/// The examples are seeded <b>inactive</b> on purpose — they appear in the admin page
/// (/admin/workflow-assignment-config) as ready-made templates, but do NOT change runtime
/// assignment until an admin toggles them Active. Activity ids + baseline groups below match
/// appraisal-workflow.json.
///
/// The pending-approval rule is seeded <b>active</b>: Conflicted members (the Sign-off staff of the summary
/// report) get no committee approval task. It is checked on its own because the
/// 20261030120000_SeedData_PendingApprovalConflictedMembers script, which is what reaches databases outside
/// Development, usually inserts it first (migrate runs before the app starts).
/// </summary>
public class TaskAssignmentConfigSeeder(
    WorkflowDbContext context,
    ILogger<TaskAssignmentConfigSeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    private const string PendingApproval = "pending-approval";

    public async Task SeedAllAsync()
    {
        var rows = new List<TaskAssignmentConfiguration>();

        if (!await context.TaskAssignmentConfigurations.AnyAsync(c => c.ActivityId != PendingApproval))
        {
            // Retail: route the admin-assignment activity through the pool instead of the JSON default.
            rows.Add(BuildInactive(
                activityId: "appraisal-assignment",
                bankingSegment: "Retail",
                assigneeGroup: "IntAdmin",
                primaryStrategies: ["pool"],
                routeBackStrategies: ["previous_owner"]));
        }

        if (!await context.TaskAssignmentConfigurations.AnyAsync(c => c.ActivityId == PendingApproval))
        {
            // Approval runs no assignment strategies, so those stay empty.
            rows.Add(TaskAssignmentConfiguration.Create(
                PendingApproval,
                primaryStrategies: "[]",
                routeBackStrategies: "[]",
                createdBy: "system",
                escalateToAdminPool: false,
                excludeAssigneesFrom: JsonSerializer.Serialize(new[]
                {
                    "int-appraisal-execution", "appraisal-book-verification", "int-offline-book-keyin",
                    "int-appraisal-check", "int-appraisal-verification"
                })));
        }

        if (rows.Count == 0)
        {
            logger.LogInformation("TaskAssignmentConfigurations already seeded, skipping");
            return;
        }

        context.TaskAssignmentConfigurations.AddRange(rows);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} TaskAssignmentConfiguration rows", rows.Count);
    }

    private static TaskAssignmentConfiguration BuildInactive(
        string activityId,
        string bankingSegment,
        string assigneeGroup,
        string[] primaryStrategies,
        string[] routeBackStrategies)
    {
        // Seed disabled — admin opts in by toggling Active in the UI.
        return TaskAssignmentConfiguration.Create(
            activityId,
            JsonSerializer.Serialize(primaryStrategies),
            JsonSerializer.Serialize(routeBackStrategies),
            createdBy: "system",
            workflowDefinitionId: null,
            specificAssignee: null,
            assigneeGroup: assigneeGroup,
            bankingSegment: bankingSegment,
            isActive: false);
    }
}
