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

    // Activity IDs that start the clock from the confirmed appointment date rather than AssignedAt.
    // Vendor execution: on-site visit determines the actual inspection start.
    // Internal execution: treated symmetrically for unified SLA governance.
    private static readonly HashSet<string> AppointmentAnchoredActivityIds = new(StringComparer.Ordinal)
    {
        "ext-appraisal-execution",
        "int-appraisal-execution"
    };

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

        // Back-fill AnchorType on rows that were seeded before this column was added.
        // AnchorType is an editable field (operators can override via the matrix UI), so we only
        // set it when it is null (unset) — never overwrite an intentional operator change.
        var backfilled = 0;
        foreach (var row in existing)
        {
            if (row.AnchorType.HasValue)
                continue;

            var shouldAnchor = row.Scope switch
            {
                // Activity-scope rows for ext/int-appraisal-execution are appointment-anchored.
                // Stage-scope rows use Assignment anchor (WorkflowTransitioned handler stamps AssignedAt).
                // The appointment-date clip for vendor OLA reporting is applied in PartySlaEvaluator.
                SlaPolicyScope.Activity => !string.IsNullOrEmpty(row.ActivityId) &&
                                           AppointmentAnchoredActivityIds.Contains(row.ActivityId),
                _                       => false
            };

            if (shouldAnchor)
            {
                row.SetAnchorType(SlaAnchorType.AppointmentDate);
                backfilled++;
            }
        }

        if (toAdd.Count == 0 && backfilled == 0)
        {
            logger.LogInformation(
                "All SLA policies for workflow '{Name}' already present, nothing to seed", WorkflowName);
            return;
        }

        context.SlaPolicies.AddRange(toAdd);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "SLA policy seed for workflow '{Name}': added={Added}, backfilled AnchorType={Backfilled}, skipped={Skipped}",
            WorkflowName, toAdd.Count, backfilled, candidates.Count - toAdd.Count);
    }

    private static List<SlaPolicy> BuildPolicies(Guid workflowDefinitionId)
    {
        var policies = new List<SlaPolicy>();

        // Activity scope (11 rows) — mirror timeoutDuration from appraisal-workflow.json.
        // Execution activities are appointment-anchored: the SLA clock starts from the confirmed
        // on-site visit, not from AssignedAt. This prevents re-assignments from granting a fresh window.
        var activityHours = new (string ActivityId, int Hours, SlaAnchorType? AnchorType)[]
        {
            ("appraisal-initiation-check",  48, null),
            ("appraisal-initiation",        48, null),
            ("appraisal-assignment",        72, null),
            ("ext-appraisal-assignment",    48, null),
            ("ext-appraisal-execution",     72, SlaAnchorType.AppointmentDate),
            ("ext-appraisal-check",         48, null),
            ("ext-appraisal-verification",  24, null),
            ("int-appraisal-execution",     72, SlaAnchorType.AppointmentDate),
            ("int-appraisal-check",         48, null),
            ("int-appraisal-verification",  24, null),
            ("appraisal-book-verification", 72, null),
        };
        foreach (var (activityId, hours, anchorType) in activityHours)
        {
            policies.Add(SlaPolicy.Create(
                activityId: activityId,
                durationHours: hours,
                useBusinessDays: true,
                priority: 100,
                workflowDefinitionId: workflowDefinitionId,
                scope: SlaPolicyScope.Activity,
                anchorType: anchorType));
        }

        // Stage scope (2 rows).
        // Vendor window (ext-appraisal-assignment → ext-appraisal-verification): Appointment-anchored,
        // 24h — the external company's total turnaround, measured from the on-site visit. Members =
        // ext-assignment / -execution / -check / -verification. SLADueDate is stamped at stage entry by
        // WorkflowTransitionedIntegrationEventHandler, which supplies the appointment (E2) so this
        // appointment-anchored window can compute `appointment + 24h` (else it would stamp null).
        policies.Add(SlaPolicy.Create(
            activityId: "*",
            durationHours: 24,
            useBusinessDays: true,
            priority: 100,
            workflowDefinitionId: workflowDefinitionId,
            scope: SlaPolicyScope.Stage,
            startActivityKey: "ext-appraisal-assignment",
            endActivityKey: "ext-appraisal-verification",
            middleActivityKeys: "[\"ext-appraisal-execution\",\"ext-appraisal-check\"]",
            anchorType: SlaAnchorType.AppointmentDate));

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
