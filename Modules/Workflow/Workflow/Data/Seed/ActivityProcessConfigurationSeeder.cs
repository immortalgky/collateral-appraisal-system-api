using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data.Seed;
using Workflow.Data.Entities;

namespace Workflow.Data.Seed;

public class ActivityProcessConfigurationSeeder(
    WorkflowDbContext context,
    ILogger<ActivityProcessConfigurationSeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    public async Task SeedAllAsync()
    {
        if (await context.ActivityProcessConfigurations.AnyAsync())
        {
            logger.LogInformation("Activity process configurations already seeded, skipping...");
            return;
        }

        logger.LogInformation("Seeding activity process configurations...");

        var configs = new List<ActivityProcessConfiguration>
        {
            // site-inspection: validate value, then update appraisal to UnderReview, then complete assignment
            ActivityProcessConfiguration.Create(
                "site-inspection",
                "Validate appraised value",
                "ValidateHasAppraisedValue",
                1,
                "system"),
            ActivityProcessConfiguration.Create(
                "site-inspection",
                "Update appraisal status to UnderReview",
                "UpdateAppraisalStatus",
                2,
                "system",
                """{"targetStatus": "UnderReview"}"""),
            ActivityProcessConfiguration.Create(
                "site-inspection",
                "Complete assignment",
                "UpdateAssignmentStatus",
                3,
                "system",
                """{"targetStatus": "Completed"}"""),

            // appraisal-assignment: validate decision constraints before routing
            ActivityProcessConfiguration.Create(
                "appraisal-assignment",
                "Validate Decision Constraints",
                "ValidateDecisionConstraints",
                1,
                "system",
                """{"decisionField":"decisionTaken","constraints":{"INT":"facilityLimit <= 50000000"}}"""),

            // admin-review: update appraisal to InProgress
            ActivityProcessConfiguration.Create(
                "admin-review",
                "Start appraisal work",
                "UpdateAppraisalStatus",
                1,
                "system",
                """{"targetStatus": "InProgress"}"""),

            // __on_workflow_start__: trigger immediate appraisal creation for non-manual channels
            ActivityProcessConfiguration.Create(
                "__on_workflow_start__",
                "Emit appraisal creation (non-manual)",
                "EmitAppraisalCreationRequested",
                1,
                "system",
                """{"condition": "channel != 'MANUAL'"}"""),

            // appraisal-initiation-check: trigger deferred appraisal creation for manual channels
            ActivityProcessConfiguration.Create(
                "appraisal-initiation-check",
                "Emit appraisal creation (manual)",
                "EmitAppraisalCreationRequested",
                1,
                "system",
                """{"condition": "channel == 'MANUAL'", "requireDecision": "P"}"""),
        };

        await context.ActivityProcessConfigurations.AddRangeAsync(configs);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} activity process configurations", configs.Count);
    }
}
