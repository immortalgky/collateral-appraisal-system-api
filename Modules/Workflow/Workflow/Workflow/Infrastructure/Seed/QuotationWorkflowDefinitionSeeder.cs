using Shared.Data.Seed;

namespace Workflow.Workflow.Infrastructure.Seed;

/// <summary>
/// Seeds the quotation workflow definition, plus its Published v1 version, from the embedded JSON
/// resource. Idempotent: skipped if a definition with the same name already exists.
/// </summary>
public class QuotationWorkflowDefinitionSeeder(
    WorkflowDbContext context,
    ILogger<QuotationWorkflowDefinitionSeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    public const string WorkflowName = "Quotation Workflow";

    public Task SeedAllAsync()
    {
        var json = WorkflowDefinitionSeedHelper.LoadEmbeddedResource(
            typeof(QuotationWorkflowDefinitionSeeder).Assembly,
            "Workflow.Workflow.Config.quotation-workflow.json",
            "Workflow.Workflow.Config.quotation_workflow.json");

        return WorkflowDefinitionSeedHelper.SeedAsync(
            context,
            logger,
            name: WorkflowName,
            description: "Child workflow for the RFQ (Request for Quotation) bidding process",
            category: "Appraisal",
            createdBy: "system",
            fileJson: json);
    }
}
