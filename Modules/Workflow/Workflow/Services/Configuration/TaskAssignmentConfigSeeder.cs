using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Data.Seed;
using Workflow.Data;
using Workflow.Data.Entities;

namespace Workflow.Services.Configuration;

/// <summary>
/// Seeds example <see cref="TaskAssignmentConfiguration"/> override rows, plus the active pending-approval
/// conflicted-member rule. Idempotent: skipped if any rows already exist.
///
/// The examples are seeded <b>inactive</b> on purpose — they appear in the admin page
/// (/admin/workflow-assignment-config) as ready-made templates, but do NOT change runtime
/// assignment until an admin toggles them Active. This keeps the seeder safe to run in every
/// environment. Activity ids + baseline groups below match appraisal-workflow.json.
/// </summary>
public class TaskAssignmentConfigSeeder(
    WorkflowDbContext context,
    ILogger<TaskAssignmentConfigSeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    public async Task SeedAllAsync()
    {
        if (await context.TaskAssignmentConfigurations.AnyAsync())
        {
            logger.LogInformation("TaskAssignmentConfigurations already seeded, skipping");
            return;
        }

        var examples = new[]
        {
            // Retail: route the admin-assignment activity through the pool instead of the JSON default.
            BuildInactive(
                activityId: "appraisal-assignment",
                bankingSegment: "Retail",
                assigneeGroup: "IntAdmin",
                primaryStrategies: ["pool"],
                routeBackStrategies: ["previous_owner"]),

            // Active, unlike the examples: Conflicted members (the Sign-off staff of the summary report) get no
            // committee approval task. Same value as the 20261030120000_SeedData_PendingApprovalConflictedMembers
            // script, which is what reaches databases outside Development. Approval runs no strategies.
            TaskAssignmentConfiguration.Create(
                "pending-approval",
                primaryStrategies: "[]",
                routeBackStrategies: "[]",
                createdBy: "system",
                escalateToAdminPool: false,
                excludeAssigneesFrom: JsonSerializer.Serialize(new[]
                {
                    "int-appraisal-execution", "appraisal-book-verification",
                    "int-appraisal-check", "int-appraisal-verification"
                }))
        };

        context.TaskAssignmentConfigurations.AddRange(examples);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} TaskAssignmentConfiguration rows", examples.Length);
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
