using Shared.Data.Seed;
using Workflow.Workflow.Infrastructure.Seed;

namespace Workflow.DocumentFollowups.Infrastructure;

/// <summary>
/// Seeds the document-followup workflow definition, plus its Published v1 version, from the embedded
/// JSON resource. Idempotent: skipped if a definition with the same name already exists.
/// </summary>
public class DocumentFollowupWorkflowDefinitionSeeder(
    WorkflowDbContext context,
    ILogger<DocumentFollowupWorkflowDefinitionSeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    public const string WorkflowName = "Document Followup Workflow";

    public Task SeedAllAsync()
    {
        var json = WorkflowDefinitionSeedHelper.LoadEmbeddedResource(
            typeof(DocumentFollowupWorkflowDefinitionSeeder).Assembly,
            "Workflow.Workflow.Config.document-followup-workflow.json",
            "Workflow.Workflow.Config.document_followup_workflow.json");

        return WorkflowDefinitionSeedHelper.SeedAsync(
            context,
            logger,
            name: WorkflowName,
            description: "Out-of-band followup workflow for document requests",
            category: "Appraisal",
            createdBy: "system",
            fileJson: json);
    }
}
