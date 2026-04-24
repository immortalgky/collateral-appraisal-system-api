using Shared.Data.Seed;
using Workflow.Workflow.Models;

namespace Workflow.Workflow.Infrastructure.Seed;

/// <summary>
/// Seeds the quotation workflow definition from the embedded JSON resource.
/// Idempotent: skipped if a definition with the same name already exists.
/// </summary>
public class QuotationWorkflowDefinitionSeeder(
    WorkflowDbContext context,
    ILogger<QuotationWorkflowDefinitionSeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    public const string WorkflowName = "Quotation Workflow";
    private const string ResourceName = "Workflow.Workflow.Config.quotation_workflow.json";

    public async Task SeedAllAsync()
    {
        if (await context.WorkflowDefinitions.AnyAsync(x => x.Name == WorkflowName))
        {
            logger.LogInformation("Quotation workflow definition already seeded, skipping");
            return;
        }

        var json = LoadEmbeddedResource();
        if (json is null)
        {
            logger.LogWarning("Quotation workflow JSON resource not found");
            return;
        }

        var definition = WorkflowDefinition.Create(
            name: WorkflowName,
            description: "Child workflow for the RFQ (Request for Quotation) bidding process",
            jsonDefinition: json,
            category: "Appraisal",
            createdBy: "system");

        context.WorkflowDefinitions.Add(definition);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded quotation workflow definition {Id}", definition.Id);
    }

    private static string? LoadEmbeddedResource()
    {
        var assembly = typeof(QuotationWorkflowDefinitionSeeder).Assembly;
        // Try both hyphen and underscore variants — embedded resource names depend on
        // how the build replaces invalid identifier characters.
        foreach (var candidate in new[]
                 {
                     "Workflow.Workflow.Config.quotation-workflow.json",
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
