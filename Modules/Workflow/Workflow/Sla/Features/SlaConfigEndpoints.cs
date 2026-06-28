using System.Text.Json;
using Carter;
using Microsoft.EntityFrameworkCore;
using Workflow.Data;
using Workflow.Sla.Models;
using Workflow.Sla.Services;

namespace Workflow.Sla.Features;

public class SlaConfigEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // All SLA/OLA configuration is admin-only (manage the org-wide targets).
        var group = app.MapGroup("/api/sla/configurations").WithTags("SLA Configuration")
            .RequireAuthorization("sla-config.manage");

        group.MapGet("/", GetConfigurations);
        group.MapGet("/matrix", GetMatrix);
        group.MapPost("/", CreateConfiguration);
        group.MapPut("/{id:guid}", UpdateConfiguration);
        group.MapDelete("/{id:guid}", DeleteConfiguration);

        // Holidays
        var holidays = app.MapGroup("/api/sla/holidays").WithTags("SLA Configuration")
            .RequireAuthorization("sla-config.manage");
        holidays.MapGet("/", GetHolidays);
        holidays.MapPost("/", CreateHoliday);
        holidays.MapDelete("/{id:guid}", DeleteHoliday);

        // Business hours
        var bh = app.MapGroup("/api/sla/business-hours").WithTags("SLA Configuration")
            .RequireAuthorization("sla-config.manage");
        bh.MapGet("/", GetBusinessHours);
        bh.MapPost("/", UpsertBusinessHours);
    }

    // --- SLA Configurations ---
    private static async Task<IResult> GetConfigurations(WorkflowDbContext db)
    {
        var configs = await db.SlaPolicies
            .AsNoTracking()
            .OrderBy(c => c.Priority)
            .Select(c => new SlaConfigDto(
                c.Id, c.ActivityId, c.WorkflowDefinitionId, c.CompanyId,
                c.LoanType, c.DurationHours, c.UseBusinessDays, c.Priority,
                c.AppraisalType, c.Scope, c.StartActivityKey, c.EndActivityKey, c.MiddleActivityKeys,
                c.AnchorType))
            .ToListAsync();
        return Results.Ok(configs);
    }

    // Selector-driven matrix read for the admin screen: for a (loanType, appraisalType) path it
    // returns the effective SLA umbrella, the group (Stage-scope) OLAs, and the per-activity OLA rows.
    // Only company-agnostic config rows (CompanyId = null) participate — the screen edits segment/type,
    // not per-company overrides. "Effective" resolution mirrors the calculator: lower Priority wins,
    // null dimensions act as wildcards. IsOverride = a row keyed to this exact cell exists.
    private static async Task<IResult> GetMatrix(WorkflowDbContext db, string? loanType, string? appraisalType)
    {
        var policies = await db.SlaPolicies
            .AsNoTracking()
            .Where(p => p.CompanyId == null)
            .OrderBy(p => p.Priority)
            .ToListAsync();

        bool MatchesCell(SlaPolicy p) =>
            (p.LoanType == null || p.LoanType == loanType)
            && (p.AppraisalType == null || p.AppraisalType == appraisalType);
        bool IsExactCell(SlaPolicy p) => p.LoanType == loanType && p.AppraisalType == appraisalType;

        // Umbrella (Workflow scope). There is one appraisal workflow, so resolve ignoring definition id.
        // Selection mirrors SlaCalculator: lowest Priority wins, ties broken toward the more specific row.
        var umbrellaPolicy = policies
            .Where(p => p.Scope == SlaPolicyScope.Workflow && MatchesCell(p))
            .OrderBy(p => p.Priority)
            .ThenByDescending(p => p.LoanType != null)
            .ThenByDescending(p => p.AppraisalType != null)
            .FirstOrDefault();
        var umbrella = new SlaMatrixUmbrella(
            umbrellaPolicy?.Id, umbrellaPolicy?.WorkflowDefinitionId, umbrellaPolicy?.DurationHours,
            umbrellaPolicy?.UseBusinessDays ?? true,
            umbrellaPolicy is not null && IsExactCell(umbrellaPolicy));

        // Load workflow transitions needed for graph-walk member resolution.
        // Collect distinct WorkflowDefinitionIds from Stage policies and load their JsonDefinitions.
        var stagePolicyDefinitionIds = policies
            .Where(p => p.Scope == SlaPolicyScope.Stage && p.WorkflowDefinitionId.HasValue)
            .Select(p => p.WorkflowDefinitionId!.Value)
            .Distinct()
            .ToList();

        var transitionsByDefinitionId =
            new Dictionary<Guid, IReadOnlyList<(string From, string To, string? Condition)>>();

        if (stagePolicyDefinitionIds.Count > 0)
        {
            var definitions = await db.WorkflowDefinitions
                .AsNoTracking()
                .Where(d => stagePolicyDefinitionIds.Contains(d.Id))
                .Select(d => new { d.Id, d.JsonDefinition })
                .ToListAsync();

            foreach (var def in definitions)
            {
                transitionsByDefinitionId[def.Id] =
                    WorkflowGraphHelper.GetOrParseTransitions(def.Id, def.JsonDefinition);
            }
        }

        // Group (Stage scope) OLAs that apply to this cell.
        var groups = policies
            .Where(p => p.Scope == SlaPolicyScope.Stage && MatchesCell(p) && p.StartActivityKey != null)
            .GroupBy(p => p.StartActivityKey)
            .Select(g => g.OrderBy(p => p.Priority)
                .ThenByDescending(p => p.LoanType != null)
                .ThenByDescending(p => p.AppraisalType != null)
                .First())
            .Select(p =>
            {
                var spanActivities = GroupSpanActivities(
                    p.StartActivityKey!, p.EndActivityKey, p.MiddleActivityKeys,
                    p.WorkflowDefinitionId, transitionsByDefinitionId).ToList();
                return new SlaMatrixGroup(
                    p.Id, p.StartActivityKey!, p.EndActivityKey, p.MiddleActivityKeys,
                    p.DurationHours, p.UseBusinessDays, IsExactCell(p),
                    OwnerOf(p.StartActivityKey!), ScenarioOf(p.StartActivityKey!),
                    p.AnchorType, spanActivities);
            })
            .ToList();

        // Which activities are covered by a defined group span (inner targets, not summed).
        // Also record the governing window (group start key) for clockMode computation.
        var coveredActivities = new HashSet<string>();
        // Maps activityId → group StartActivityKey (governing window).
        var governingWindowMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var g in groups)
        {
            foreach (var a in g.Members)
            {
                coveredActivities.Add(a);
                // Track governing window: an appointment-anchored group governs the clock mode.
                if (g.AnchorType == SlaAnchorType.AppointmentDate && !governingWindowMap.ContainsKey(a))
                    governingWindowMap[a] = g.StartActivityKey;
            }
        }

        // Per-activity OLA rows from the static catalog.
        var activities = ActivityCatalog.Select(cat =>
        {
            var match = policies
                .Where(p => p.Scope == SlaPolicyScope.Activity
                            && (p.ActivityId == cat.Id || p.ActivityId == "*")
                            && MatchesCell(p))
                .OrderBy(p => p.Priority)
                .ThenByDescending(p => p.ActivityId == cat.Id)
                .ThenByDescending(p => p.LoanType != null)
                .ThenByDescending(p => p.AppraisalType != null)
                .FirstOrDefault();

            // clockMode = "WindowMember" when the activity is inside an appointment-anchored group;
            // "OwnClock" otherwise. GoverningWindow = the group's StartActivityKey.
            var governingWindow = governingWindowMap.TryGetValue(cat.Id, out var gw) ? gw : null;
            var clockMode = governingWindow is not null ? "WindowMember" : "OwnClock";

            return new SlaMatrixActivity(
                cat.Id, cat.Name, cat.Owner, cat.Scenario,
                match?.Id, match?.DurationHours, match?.UseBusinessDays ?? true,
                match is not null && IsExactCell(match) && match.ActivityId == cat.Id,
                coveredActivities.Contains(cat.Id),
                match?.AnchorType, clockMode, governingWindow);
        }).ToList();

        return Results.Ok(new SlaMatrixResponse(loanType, appraisalType, umbrella, groups, activities));
    }

    // Owner = who is responsible for the time (display group + which OLA total): External(vendor) for
    // ext-*; Bank for the bank-staff appraisal step; Shared for bank steps common to both cases.
    // Scenario = which mutually-exclusive case the activity runs in (Σ membership).
    private static ActivityCatalogEntry? CatalogOf(string activityId) =>
        ActivityCatalog.FirstOrDefault(a => a.Id == activityId);

    private static string OwnerOf(string activityId) => CatalogOf(activityId)?.Owner ?? "Shared";

    private static string ScenarioOf(string activityId) => CatalogOf(activityId)?.Scenario ?? "Both";

    // Activities covered by a group span.
    // When MiddleActivityKeys is persisted (a JSON array) it is authoritative:
    // covered = start ∪ middle ∪ end (handles non-contiguous or custom spans).
    // Otherwise, walks the workflow's forward transition graph from start to end via
    // WorkflowGraphHelper — the same logic used by SlaCalculator.BuildStageActivityIdsAsync
    // so engine and admin screen always agree.
    private static IEnumerable<string> GroupSpanActivities(
        string start, string? end, string? middleJson,
        Guid? workflowDefinitionId,
        IReadOnlyDictionary<Guid, IReadOnlyList<(string From, string To, string? Condition)>> transitionsByDefinitionId)
    {
        var middle = ParseMiddleKeys(middleJson);
        if (middle is not null)
        {
            var covered = new HashSet<string>(middle) { start };
            if (end is not null) covered.Add(end);
            return covered;
        }

        // Graph-walk fallback: forward path start → end in the workflow transition graph.
        if (end is not null && workflowDefinitionId.HasValue
            && transitionsByDefinitionId.TryGetValue(workflowDefinitionId.Value, out var transitions))
        {
            return WorkflowGraphHelper.GetForwardPathActivityIds(transitions, start, end);
        }

        // No workflow definition or end key available — return at least the start activity.
        return new[] { start };
    }

    private static List<string>? ParseMiddleKeys(string? middleJson)
    {
        if (string.IsNullOrWhiteSpace(middleJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<string>>(middleJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Static catalog of the appraisal workflow's work activities that can carry OLA targets.
    // Owner = display group + OLA attribution (Shared bank / External vendor / Bank appraisal step).
    // Scenario = which mutually-exclusive case it runs in (Both / ExternalCase / InHouseCase).
    // Note: appraisal-book-verification is BANK staff time (Owner=Bank), never counted in the vendor
    // (External) OLA, even though it only runs in the external-company case. int-appraisal-check and
    // int-appraisal-verification run in BOTH cases, so they are Shared. Order follows the flow.
    private static readonly IReadOnlyList<ActivityCatalogEntry> ActivityCatalog = new List<ActivityCatalogEntry>
    {
        new("appraisal-initiation-check", "Appraisal Initiation Check", "Shared", "Both"),
        new("appraisal-initiation", "Appraisal Initiation", "Shared", "Both"),
        new("int-pma-input", "PMA Property Input", "Shared", "Both"),
        new("appraisal-assignment", "Appraisal Assignment", "Shared", "Both"),
        new("ext-appraisal-assignment", "External Appraisal Assignment", "External", "ExternalCase"),
        new("ext-appraisal-execution", "External Appraisal Execution", "External", "ExternalCase"),
        new("ext-appraisal-check", "External Appraisal Check", "External", "ExternalCase"),
        new("ext-appraisal-verification", "External Appraisal Verification", "External", "ExternalCase"),
        new("appraisal-book-verification", "Appraisal Book Verification", "Bank", "ExternalCase"),
        new("int-appraisal-execution", "Internal Appraisal Execution", "Bank", "InHouseCase"),
        new("int-appraisal-check", "Internal Appraisal Check", "Shared", "Both"),
        new("int-appraisal-verification", "Internal Appraisal Verification", "Shared", "Both"),
        new("pending-meeting", "Pending Meeting", "Shared", "Both"),
        new("pending-approval", "Committee Approval", "Shared", "Both"),
    };

    private static async Task<IResult> CreateConfiguration(CreateSlaConfigRequest request, WorkflowDbContext db)
    {
        try
        {
            var config = SlaPolicy.Create(
                request.ActivityId, request.DurationHours, request.UseBusinessDays,
                request.Priority, request.WorkflowDefinitionId, request.CompanyId, request.LoanType,
                request.Scope, request.StartActivityKey, request.EndActivityKey,
                request.MiddleActivityKeys, request.AppraisalType, request.AnchorType);
            db.SlaPolicies.Add(config);
            await db.SaveChangesAsync();
            return Results.Created($"/api/sla/configurations/{config.Id}", ToDto(config));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static SlaConfigDto ToDto(SlaPolicy c) => new(
        c.Id, c.ActivityId, c.WorkflowDefinitionId, c.CompanyId, c.LoanType,
        c.DurationHours, c.UseBusinessDays, c.Priority, c.AppraisalType, c.Scope,
        c.StartActivityKey, c.EndActivityKey, c.MiddleActivityKeys, c.AnchorType);

    private static async Task<IResult> UpdateConfiguration(Guid id, UpdateSlaConfigRequest request, WorkflowDbContext db)
    {
        var config = await db.SlaPolicies.FindAsync(id);
        if (config is null) return Results.NotFound();
        try
        {
            config.Update(
                request.DurationHours, request.UseBusinessDays, request.Priority,
                request.LoanType, request.CompanyId,
                request.Scope, request.StartActivityKey, request.EndActivityKey,
                request.MiddleActivityKeys, request.WorkflowDefinitionId, request.AppraisalType,
                request.AnchorType);
            await db.SaveChangesAsync();
            return Results.Ok();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> DeleteConfiguration(Guid id, WorkflowDbContext db)
    {
        var config = await db.SlaPolicies.FindAsync(id);
        if (config is null) return Results.NotFound();
        db.SlaPolicies.Remove(config);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // --- Holidays ---
    private static async Task<IResult> GetHolidays(WorkflowDbContext db, int? year)
    {
        var query = db.Holidays.AsNoTracking();
        if (year.HasValue)
            query = query.Where(h => h.Year == year.Value);
        var holidays = await query.OrderBy(h => h.Date).ToListAsync();
        return Results.Ok(holidays);
    }

    private static async Task<IResult> CreateHoliday(CreateHolidayRequest request, WorkflowDbContext db)
    {
        var holiday = Holiday.Create(request.Date, request.Description);
        db.Holidays.Add(holiday);
        await db.SaveChangesAsync();
        return Results.Created($"/api/sla/holidays/{holiday.Id}", holiday);
    }

    private static async Task<IResult> DeleteHoliday(Guid id, WorkflowDbContext db)
    {
        var holiday = await db.Holidays.FindAsync(id);
        if (holiday is null) return Results.NotFound();
        db.Holidays.Remove(holiday);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // --- Business Hours ---
    private static async Task<IResult> GetBusinessHours(WorkflowDbContext db)
    {
        var config = await db.BusinessHoursConfigs.AsNoTracking().FirstOrDefaultAsync(b => b.IsActive);
        return config is null ? Results.NotFound() : Results.Ok(config);
    }

    private static async Task<IResult> UpsertBusinessHours(UpsertBusinessHoursRequest request, WorkflowDbContext db)
    {
        try
        {
            var existing = await db.BusinessHoursConfigs.FirstOrDefaultAsync(b => b.IsActive);
            if (existing is not null)
            {
                existing.Update(request.StartTime, request.EndTime, request.TimeZone, true,
                    request.LunchStartTime, request.LunchEndTime);
            }
            else
            {
                var config = BusinessHoursConfig.Create(request.StartTime, request.EndTime, request.TimeZone,
                    request.LunchStartTime, request.LunchEndTime);
                db.BusinessHoursConfigs.Add(config);
            }
            await db.SaveChangesAsync();
            return Results.Ok();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}

// Request DTOs
public record CreateSlaConfigRequest(
    string ActivityId, int DurationHours, bool UseBusinessDays, int Priority,
    Guid? WorkflowDefinitionId = null, Guid? CompanyId = null, string? LoanType = null,
    string? AppraisalType = null, SlaPolicyScope Scope = SlaPolicyScope.Activity,
    string? StartActivityKey = null, string? EndActivityKey = null, string? MiddleActivityKeys = null,
    SlaAnchorType? AnchorType = null);

public record UpdateSlaConfigRequest(
    int DurationHours, bool UseBusinessDays, int Priority,
    string? LoanType = null, Guid? CompanyId = null,
    SlaPolicyScope? Scope = null, string? StartActivityKey = null,
    string? EndActivityKey = null, string? MiddleActivityKeys = null,
    Guid? WorkflowDefinitionId = null, string? AppraisalType = null,
    SlaAnchorType? AnchorType = null);

public record SlaConfigDto(
    Guid Id, string ActivityId, Guid? WorkflowDefinitionId, Guid? CompanyId,
    string? LoanType, int DurationHours, bool UseBusinessDays, int Priority,
    string? AppraisalType, SlaPolicyScope Scope,
    string? StartActivityKey, string? EndActivityKey, string? MiddleActivityKeys,
    SlaAnchorType? AnchorType);

// Matrix read DTOs (admin screen)
public record SlaMatrixResponse(
    string? LoanType, string? AppraisalType,
    SlaMatrixUmbrella Umbrella,
    IReadOnlyList<SlaMatrixGroup> Groups,
    IReadOnlyList<SlaMatrixActivity> Activities);

public record SlaMatrixUmbrella(
    Guid? PolicyId, Guid? WorkflowDefinitionId, int? DurationHours, bool UseBusinessDays, bool IsOverride);

public record SlaMatrixGroup(
    Guid PolicyId, string StartActivityKey, string? EndActivityKey, string? MiddleActivityKeys,
    int DurationHours, bool UseBusinessDays, bool IsOverride, string Owner, string Scenario,
    // AnchorType: null or Assignment = group clock starts at the first task AssignedAt;
    //             AppointmentDate = group clock starts at the confirmed on-site visit.
    SlaAnchorType? AnchorType,
    // Members: activity IDs that fall inside this stage span (start ∪ middle ∪ end).
    IReadOnlyList<string> Members);

// Owner = display group / OLA attribution (Shared | External | Bank).
// Scenario = which mutually-exclusive case it runs in (Both | ExternalCase | InHouseCase).
public record SlaMatrixActivity(
    string ActivityId, string Name, string Owner, string Scenario,
    Guid? PolicyId, int? DurationHours, bool UseBusinessDays, bool IsOverride, bool CoveredByGroup,
    // AnchorType from the matched Activity-scope policy (if any).
    SlaAnchorType? AnchorType,
    // ClockMode: "OwnClock" = this activity drives its own deadline; "WindowMember" = it sits
    // inside an appointment-anchored stage window (see GoverningWindow).
    string ClockMode,
    // GoverningWindow: StartActivityKey of the Stage-scope policy that governs this activity's
    // window (null when ClockMode == "OwnClock").
    string? GoverningWindow);

internal record ActivityCatalogEntry(string Id, string Name, string Owner, string Scenario);

public record CreateHolidayRequest(DateOnly Date, string Description);

public record UpsertBusinessHoursRequest(
    TimeOnly StartTime,
    TimeOnly EndTime,
    string TimeZone,
    TimeOnly? LunchStartTime = null,
    TimeOnly? LunchEndTime = null);
