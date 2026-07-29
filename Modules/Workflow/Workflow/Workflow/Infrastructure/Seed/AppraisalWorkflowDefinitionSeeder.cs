using Shared.Data.Seed;

namespace Workflow.Workflow.Infrastructure.Seed;

/// <summary>
/// Seeds the main collateral appraisal workflow definition, plus its Published v1 version, from the
/// embedded JSON resource. Idempotent: skipped if a definition with the same name already exists, so
/// an environment whose workflow has been edited through the Workflow Builder keeps its own versions.
/// </summary>
public class AppraisalWorkflowDefinitionSeeder(
    WorkflowDbContext context,
    ILogger<AppraisalWorkflowDefinitionSeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    /// <summary>
    /// Must stay in step with the name the consumers look the definition up by
    /// (<c>RequestSubmittedIntegrationEventConsumer</c>, <c>AppraisalSlaPolicySeeder</c>,
    /// <c>ResumeParentWorkflowForRequestCommandHandler</c>).
    /// </summary>
    public const string WorkflowName = "Collateral Appraisal Workflow";

    public Task SeedAllAsync()
    {
        var json = WorkflowDefinitionSeedHelper.LoadEmbeddedResource(
            typeof(AppraisalWorkflowDefinitionSeeder).Assembly,
            "Workflow.Workflow.Config.appraisal-workflow.json",
            "Workflow.Workflow.Config.appraisal_workflow.json");

        return WorkflowDefinitionSeedHelper.SeedAsync(
            context,
            logger,
            name: WorkflowName,
            description: "Complete appraisal workflow from initiation check through approval",
            category: "Appraisal",
            createdBy: "system",
            fileJson: json);
    }
}
