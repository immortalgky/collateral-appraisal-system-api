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
            // site-inspection: validate value before allowing the activity to complete.
            // Assignment-status transitions are now driven by the WorkflowTransitionedIntegrationEvent
            // consumer in the Appraisal module — terminal Completion is owned by the committee
            // approval handler, not by a synchronous UpdateAssignmentStatus step here.
            ActivityProcessConfiguration.Create(
                "site-inspection",
                "Validate appraised value",
                "ValidateHasAppraisedValue",
                1,
                "system"),

            // appraisal-assignment: validate decision constraints before routing
            ActivityProcessConfiguration.Create(
                "appraisal-assignment",
                "Validate Decision Constraints",
                "ValidateDecisionConstraints",
                1,
                "system",
                """{"decisionField":"decisionTaken","constraints":{"INT":"facilityLimit <= 50000000"}}"""),

            // site-inspection (sortOrder 2): generic field rules demonstrating ValidateAppraisalFields.
            // Requires that facilityLimit is present before the site-inspection step completes.
            ActivityProcessConfiguration.Create(
                "site-inspection",
                "Validate Appraisal Fields",
                "ValidateAppraisalFields",
                StepKind.Validation,
                2,
                "system",
                """{"rules":[{"fieldKey":"facilityLimit","op":"Required","message":"Facility limit must be set before completing site inspection."}],"mode":"AllMustPass"}"""),

            // ext-appraisal-assignment: per-property mandatory fields for land and condo types.
            // Demonstrates ValidatePropertyMandatoryFields with type-specific field requirements.
            ActivityProcessConfiguration.Create(
                "ext-appraisal-assignment",
                "Validate Property Mandatory Fields",
                "ValidatePropertyMandatoryFields",
                StepKind.Validation,
                1,
                "system",
                """{"requiredByType":{"L":["TitleNumber","LandOffice","Province"],"U":["Province","District"]}}"""),
        };

        await context.ActivityProcessConfigurations.AddRangeAsync(configs);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} activity process configurations", configs.Count);
    }
}
