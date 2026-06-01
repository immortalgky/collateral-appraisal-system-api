using Shared.Data.Seed;
using Workflow.Workflow.Models;

namespace Workflow.FeeAppointmentApprovals.Infrastructure;

/// <summary>
/// Seeds the fee-appointment approval workflow definition from the embedded JSON resource.
/// Idempotent: skipped if a definition with the same name already exists.
/// </summary>
public class FeeAppointmentApprovalWorkflowDefinitionSeeder(
    WorkflowDbContext context,
    ILogger<FeeAppointmentApprovalWorkflowDefinitionSeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    public const string WorkflowName = "Fee Appointment Approval Workflow";
    private const string ResourceName = "Workflow.Workflow.Config.fee-appointment-approval-workflow.json";

    public async Task SeedAllAsync()
    {
        if (await context.WorkflowDefinitions.AnyAsync(x => x.Name == WorkflowName))
        {
            logger.LogInformation("Fee appointment approval workflow definition already seeded, skipping");
            return;
        }

        var json = LoadEmbeddedResource();
        if (json is null)
        {
            logger.LogWarning("Fee appointment approval workflow JSON resource not found");
            return;
        }

        var definition = WorkflowDefinition.Create(
            name: WorkflowName,
            description: "Approval workflow for external company fee and appointment changes",
            jsonDefinition: json,
            category: "Appraisal",
            createdBy: "system");

        context.WorkflowDefinitions.Add(definition);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded fee appointment approval workflow definition {Id}", definition.Id);
    }

    private static string? LoadEmbeddedResource()
    {
        var assembly = typeof(FeeAppointmentApprovalWorkflowDefinitionSeeder).Assembly;
        foreach (var candidate in new[]
                 {
                     "Workflow.Workflow.Config.fee-appointment-approval-workflow.json",
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
