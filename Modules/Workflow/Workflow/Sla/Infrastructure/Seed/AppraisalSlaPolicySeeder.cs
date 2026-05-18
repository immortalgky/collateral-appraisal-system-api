using Microsoft.EntityFrameworkCore;
using Shared.Data.Seed;
using Workflow.Data;
using Workflow.Sla.Models;

namespace Workflow.Sla.Infrastructure.Seed;

public class AppraisalSlaPolicySeeder(
    WorkflowDbContext context,
    ILogger<AppraisalSlaPolicySeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    private const string WorkflowName = "Collateral Appraisal Workflow";

    public async Task SeedAllAsync()
    {
        var workflow = await context.WorkflowDefinitions
            .FirstOrDefaultAsync(x => x.Name == WorkflowName);

        if (workflow is null)
        {
            logger.LogWarning(
                "Skipping SLA policy seed — workflow definition '{Name}' not found. " +
                "Re-run after the workflow definition is created.",
                WorkflowName);
            return;
        }

        // Load all existing policies for this workflow once — used for per-row duplicate checks.
        var existing = await context.SlaPolicies
            .Where(p => p.WorkflowDefinitionId == workflow.Id)
            .ToListAsync();

        var candidates = BuildPolicies(workflow.Id);
        var toAdd = new List<SlaPolicy>();

        foreach (var candidate in candidates)
        {
            var isDuplicate = candidate.Scope switch
            {
                SlaPolicyScope.Activity => existing.Any(e =>
                    e.Scope == SlaPolicyScope.Activity &&
                    e.ActivityId == candidate.ActivityId &&
                    e.WorkflowDefinitionId == candidate.WorkflowDefinitionId &&
                    e.CompanyId == candidate.CompanyId &&
                    e.LoanType == candidate.LoanType &&
                    e.Priority == candidate.Priority),

                SlaPolicyScope.Stage => existing.Any(e =>
                    e.Scope == SlaPolicyScope.Stage &&
                    e.StartActivityKey == candidate.StartActivityKey &&
                    e.WorkflowDefinitionId == candidate.WorkflowDefinitionId &&
                    e.CompanyId == candidate.CompanyId &&
                    e.LoanType == candidate.LoanType &&
                    e.Priority == candidate.Priority),

                SlaPolicyScope.Workflow => existing.Any(e =>
                    e.Scope == SlaPolicyScope.Workflow &&
                    e.WorkflowDefinitionId == candidate.WorkflowDefinitionId &&
                    e.LoanType == candidate.LoanType),

                _ => false
            };

            if (isDuplicate)
                logger.LogDebug(
                    "Skipping existing SLA policy: Scope={Scope} ActivityId={ActivityId} StartKey={StartKey}",
                    candidate.Scope, candidate.ActivityId, candidate.StartActivityKey);
            else
                toAdd.Add(candidate);
        }

        if (toAdd.Count == 0)
        {
            logger.LogInformation(
                "All SLA policies for workflow '{Name}' already present, nothing to seed", WorkflowName);
            return;
        }

        context.SlaPolicies.AddRange(toAdd);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Seeded {Added} SLA policies for workflow '{Name}' ({Skipped} already existed)",
            toAdd.Count, WorkflowName, candidates.Count - toAdd.Count);
    }

    private static List<SlaPolicy> BuildPolicies(Guid workflowDefinitionId)
    {
        var policies = new List<SlaPolicy>();

        // Activity scope (11 rows) — mirror timeoutDuration from appraisal-workflow.json
        var activityHours = new (string ActivityId, int Hours)[]
        {
            ("appraisal-initiation-check",  48),
            ("appraisal-initiation",        48),
            ("appraisal-assignment",        72),
            ("ext-appraisal-assignment",    48),
            ("ext-appraisal-execution",     72),
            ("ext-appraisal-check",         48),
            ("ext-appraisal-verification",  24),
            ("int-appraisal-execution",     72),
            ("int-appraisal-check",         48),
            ("int-appraisal-verification",  24),
            ("appraisal-book-verification", 72),
        };
        foreach (var (activityId, hours) in activityHours)
        {
            policies.Add(SlaPolicy.Create(
                activityId: activityId,
                durationHours: hours,
                useBusinessDays: true,
                priority: 100,
                workflowDefinitionId: workflowDefinitionId,
                scope: SlaPolicyScope.Activity));
        }

        // Stage scope (2 rows)
        policies.Add(SlaPolicy.Create(
            activityId: "*",
            durationHours: 144,   // 18 business-days (8h convention) for ext window
            useBusinessDays: true,
            priority: 100,
            workflowDefinitionId: workflowDefinitionId,
            scope: SlaPolicyScope.Stage,
            startActivityKey: "ext-appraisal-execution",
            endActivityKey: "ext-appraisal-verification",
            middleActivityKeys: "[\"ext-appraisal-check\"]"));

        policies.Add(SlaPolicy.Create(
            activityId: "*",
            durationHours: 72,    // 9 business-days for int single-activity stage
            useBusinessDays: true,
            priority: 100,
            workflowDefinitionId: workflowDefinitionId,
            scope: SlaPolicyScope.Stage,
            startActivityKey: "int-appraisal-execution",
            endActivityKey: "int-appraisal-execution"));

        // Workflow scope (1 row)
        policies.Add(SlaPolicy.Create(
            activityId: "*",
            durationHours: 240,   // 30 business-days end-to-end
            useBusinessDays: true,
            priority: 100,
            workflowDefinitionId: workflowDefinitionId,
            scope: SlaPolicyScope.Workflow));

        return policies;
    }
}
