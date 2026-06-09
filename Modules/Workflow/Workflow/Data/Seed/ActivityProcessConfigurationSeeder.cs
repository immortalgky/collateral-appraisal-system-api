using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data.Seed;
using Workflow.Data.Entities;

namespace Workflow.Data.Seed;

/// <summary>
/// Seeds the per-activity validation pipeline for the real appraisal workflow.
///
/// Idempotent and additive: inserts a managed row only when no row exists for its
/// (ActivityName, ProcessorName) pair, so admin edits to existing rows are preserved and
/// re-running never duplicates. Two legacy demo rows are removed up front
/// (the placeholder `site-inspection` activity, and the misplaced property-mandatory-fields
/// row on `ext-appraisal-assignment` — property data is validated at *execution*, not assignment).
///
/// Design rules baked into the seed:
///  - Validate completeness where the data is ENTERED: appointment/fee/appraiser at assignment,
///    property + valuation + pricing at execution. Check/verification are review gates.
///  - Forward-only checks carry RunIf <c>activity.movement === 'F'</c> so route-back/cancel skip them.
///  - Ownership runs on every movement (no RunIf).
///  - An activity holds at most ONE ValidateAppraisalFields row (admin-PUT keys by ProcessorName):
///    hard field checks (Error) at execution; the soft comparables/photos warning at check.
/// </summary>
public class ActivityProcessConfigurationSeeder(
    WorkflowDbContext context,
    ILogger<ActivityProcessConfigurationSeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    private const string ForwardOnly = "activity.movement === 'F'";
    private const string System = "system";

    public async Task SeedAllAsync()
    {
        // ── Remove legacy demo rows (safe: system-authored placeholders) ──────────
        var legacy = await context.ActivityProcessConfigurations
            .Where(c =>
                c.ActivityName == "site-inspection"
                || (c.ActivityName == "ext-appraisal-assignment"
                    && c.ProcessorName == "ValidatePropertyMandatoryFields"
                    && c.CreatedBy == System))
            .ToListAsync();
        if (legacy.Count > 0)
        {
            context.ActivityProcessConfigurations.RemoveRange(legacy);
            logger.LogInformation("Removed {Count} legacy demo activity-process rows", legacy.Count);
        }

        // ── Desired managed rows (real workflow activities) ───────────────────────
        var desired = BuildDesiredConfigs();

        // Additive reconcile: insert only (activity, processor) pairs that don't exist yet.
        var existing = (await context.ActivityProcessConfigurations
                .Select(c => new { c.ActivityName, c.ProcessorName })
                .ToListAsync())
            .Select(x => (x.ActivityName, x.ProcessorName))
            .ToHashSet();

        var toAdd = desired
            .Where(c => !existing.Contains((c.ActivityName, c.ProcessorName)))
            .ToList();

        if (toAdd.Count > 0)
        {
            await context.ActivityProcessConfigurations.AddRangeAsync(toAdd);
        }

        await context.SaveChangesAsync();
        logger.LogInformation(
            "Activity-process seed reconciled: {Added} added, {Removed} legacy removed",
            toAdd.Count, legacy.Count);
    }

    private static List<ActivityProcessConfiguration> BuildDesiredConfigs()
    {
        var rows = new List<ActivityProcessConfiguration>();

        // ── appraisal-assignment: owner; decision constraints; no live quotation; no open followups ──
        rows.Add(Own("appraisal-assignment", 1));
        rows.Add(Validation("appraisal-assignment", "Validate Decision Constraints",
            "ValidateDecisionConstraints", 2,
            """{"decisionField":"decisionTaken","constraints":{"INT":"facilityLimit <= 50000000"}}"""));
        rows.Add(Validation("appraisal-assignment", "Require No Active Quotation",
            "RequireNoActiveQuotation", 3, null, ForwardOnly));
        rows.Add(Validation("appraisal-assignment", "Require Document Followup Cleared",
            "RequireDocumentFollowupCleared", 4, null, ForwardOnly));

        // ── ext-appraisal-assignment: owner; appointment + fee + appraiser set ──
        rows.Add(Own("ext-appraisal-assignment", 1));
        rows.Add(Fields("ext-appraisal-assignment", 2, ForwardOnly, """
            {"rules":[
              {"fieldKey":"hasAppointment","op":"Equals","value":"true","message":"Schedule an appointment before proceeding."},
              {"fieldKey":"appointmentInFuture","op":"Equals","value":"true","message":"The appointment date must be in the future."},
              {"fieldKey":"totalFeeAfterVat","op":"GreaterThan","value":"0","message":"Set the appraisal fee before proceeding."},
              {"fieldKey":"hasAssignedAppraiser","op":"Equals","value":"true","message":"Assign an appraiser before proceeding."}
            ],"mode":"AllMustPass"}
            """));

        // ── ext-appraisal-execution: owner; property data; value+pricing+identity ──
        rows.Add(Own("ext-appraisal-execution", 1));
        rows.Add(PropertyMandatory("ext-appraisal-execution", 2));
        rows.Add(Fields("ext-appraisal-execution", 3, ForwardOnly, ExecutionHardRulesJson));

        // ── ext-appraisal-check: owner; comparables/photos sufficiency (Warning) ──
        rows.Add(Own("ext-appraisal-check", 1));
        rows.Add(Fields("ext-appraisal-check", 2, ForwardOnly, CheckSoftRulesJson, StepSeverity.Warning));

        // ── ext-appraisal-verification: owner only (completeness enforced upstream) ──
        rows.Add(Own("ext-appraisal-verification", 1));

        // ── appraisal-book-verification: owner only ──
        rows.Add(Own("appraisal-book-verification", 1));

        // ── int-appraisal-execution: owner; property data; value+pricing+identity; followups cleared ──
        rows.Add(Own("int-appraisal-execution", 1));
        rows.Add(PropertyMandatory("int-appraisal-execution", 2));
        rows.Add(Fields("int-appraisal-execution", 3, ForwardOnly, ExecutionHardRulesJson));
        rows.Add(Validation("int-appraisal-execution", "Require Document Followup Cleared",
            "RequireDocumentFollowupCleared", 4, null, ForwardOnly));

        // ── int-appraisal-check: owner; comparables/photos (Warning); followups cleared ──
        rows.Add(Own("int-appraisal-check", 1));
        rows.Add(Fields("int-appraisal-check", 2, ForwardOnly, CheckSoftRulesJson, StepSeverity.Warning));
        rows.Add(Validation("int-appraisal-check", "Require Document Followup Cleared",
            "RequireDocumentFollowupCleared", 3, null, ForwardOnly));

        // ── int-appraisal-verification: owner; followups cleared ──
        rows.Add(Own("int-appraisal-verification", 1));
        rows.Add(Validation("int-appraisal-verification", "Require Document Followup Cleared",
            "RequireDocumentFollowupCleared", 2, null, ForwardOnly));

        return rows;
    }

    // Hard field checks at execution: appraised value, selected pricing method, identity completeness.
    private const string ExecutionHardRulesJson = """
        {"rules":[
          {"fieldKey":"hasNoAppraisedValue","op":"Equals","value":"false","message":"Set an appraised value before proceeding."},
          {"fieldKey":"selectedPricingMethodCount","op":"GreaterThan","value":"0","message":"Select at least one pricing method before proceeding."},
          {"fieldKey":"propsMissingTitle","op":"Equals","value":"0","message":"Every land property needs a title number."},
          {"fieldKey":"propsMissingLandOffice","op":"Equals","value":"0","message":"Every land property needs a land office."}
        ],"mode":"AllMustPass"}
        """;

    // Soft (Warning) advisories at check: enough comparables and photos.
    private const string CheckSoftRulesJson = """
        {"rules":[
          {"fieldKey":"comparableCount","op":"GreaterOrEqual","value":"3","message":"Fewer than 3 market comparables were captured."},
          {"fieldKey":"photoCount","op":"GreaterOrEqual","value":"2","message":"Fewer than 2 photos were attached."}
        ],"mode":"AllMustPass"}
        """;

    // ── Row factories ─────────────────────────────────────────────────────────

    private static ActivityProcessConfiguration Own(string activity, int sort) =>
        ActivityProcessConfiguration.Create(activity, "Validate Task Ownership",
            "ValidateTaskOwnership", StepKind.Validation, sort, System, "{}");

    private static ActivityProcessConfiguration Validation(
        string activity, string stepName, string processor, int sort,
        string? parametersJson, string? runIf = null) =>
        ActivityProcessConfiguration.Create(activity, stepName, processor,
            StepKind.Validation, sort, System, parametersJson, runIf);

    private static ActivityProcessConfiguration Fields(
        string activity, int sort, string? runIf, string parametersJson,
        StepSeverity severity = StepSeverity.Error) =>
        ActivityProcessConfiguration.Create(activity, "Validate Appraisal Fields",
            "ValidateAppraisalFields", StepKind.Validation, sort, System,
            parametersJson, runIf, severity);

    private static ActivityProcessConfiguration PropertyMandatory(string activity, int sort) =>
        ActivityProcessConfiguration.Create(activity, "Validate Property Mandatory Fields",
            "ValidatePropertyMandatoryFields", StepKind.Validation, sort, System,
            // Land (L) and Land-and-Building (LB) require title + the full address triple.
            // Condo (U) requires unit identity (room/building/floor) + land office + address triple.
            // Never require TitleNumber for U — its flag is always 0 (sourced from LandAppraisalDetails,
            // which condos don't have).
            """{"requiredByType":{"L":["TitleNumber","LandOffice","Province","District","SubDistrict"],"LB":["TitleNumber","LandOffice","Province","District","SubDistrict"],"U":["RoomNumber","BuildingNumber","FloorNumber","LandOffice","Province","District","SubDistrict"]}}""",
            ForwardOnly);
}
