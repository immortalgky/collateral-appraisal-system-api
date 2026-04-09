using Shared.Data.Seed;
using Workflow.Workflow.Models;

namespace Workflow.DocumentFollowups.Infrastructure;

/// <summary>
/// Seeds the document-followup workflow definition from the embedded JSON resource.
/// Idempotent: skipped if a definition with the same name already exists.
/// </summary>
public class DocumentFollowupWorkflowDefinitionSeeder(
    WorkflowDbContext context,
    ILogger<DocumentFollowupWorkflowDefinitionSeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    public const string WorkflowName = "Document Followup Workflow";
    private const string ResourceName = "Workflow.Workflow.Config.document_followup_workflow.json";

    public async Task SeedAllAsync()
    {
        if (await context.WorkflowDefinitions.AnyAsync(x => x.Name == WorkflowName))
        {
            logger.LogInformation("Document followup workflow definition already seeded, skipping");
            return;
        }

        var json = LoadEmbeddedResource();
        if (json is null)
        {
            logger.LogWarning("Document followup workflow JSON resource not found");
            return;
        }

        var definition = WorkflowDefinition.Create(
            name: WorkflowName,
            description: "Out-of-band followup workflow for document requests",
            jsonDefinition: json,
            category: "Appraisal",
            createdBy: "system");

        context.WorkflowDefinitions.Add(definition);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded document followup workflow definition {Id}", definition.Id);
    }

    private static string? LoadEmbeddedResource()
    {
        var assembly = typeof(DocumentFollowupWorkflowDefinitionSeeder).Assembly;
        // Try with hyphen and underscore variants — embedded resource names depend on
        // how the build replaces invalid identifier characters.
        foreach (var candidate in new[]
                 {
                     "Workflow.Workflow.Config.document-followup-workflow.json",
                     ResourceName
                 })
        {
            using var stream = assembly.GetManifestResourceStream(candidate);
            if (stream is null) continue;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        return null;
    }
}
