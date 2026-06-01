using Workflow.FeeAppointmentApprovals.Infrastructure;

namespace Workflow.FeeAppointmentApprovals.Endpoints;

// ─── Request/Response DTOs ────────────────────────────────────────────────────

public record CreateFeeApprovalTierRequest(
    decimal MinAmount,
    decimal? MaxAmount,
    string ApproverCode,
    string AssignedType,
    string TierLabel,
    int Priority,
    bool IsActive = true,
    string AppliesTo = "Ext");

public record UpdateFeeApprovalTierRequest(
    decimal MinAmount,
    decimal? MaxAmount,
    string ApproverCode,
    string AssignedType,
    string TierLabel,
    int Priority,
    bool IsActive,
    string AppliesTo);

public record FeeApprovalTierDto(
    Guid Id,
    decimal MinAmount,
    decimal? MaxAmount,
    string ApproverCode,
    string AssignedType,
    string TierLabel,
    int Priority,
    bool IsActive,
    string AppliesTo);

public record UpdateAppointmentApprovalRuleRequest(
    bool WeekendHolidayEnabled,
    bool WeekdayEnabled,
    bool LeadTimeEnabled,
    int? LeadTimeDays,
    bool RescheduleEnabled,
    int? RescheduleThreshold,
    string AppliesTo);

public record AppointmentApprovalRuleDto(
    Guid Id,
    bool WeekendHolidayEnabled,
    bool WeekdayEnabled,
    bool LeadTimeEnabled,
    int? LeadTimeDays,
    bool RescheduleEnabled,
    int? RescheduleThreshold,
    string AppliesTo);

// ─── Carter module ────────────────────────────────────────────────────────────

public class FeeApprovalConfigEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Fee Approval Tiers — require FEE_APPROVAL_CONFIG permission
        var tiers = app.MapGroup("/api/fee-approval/tiers")
            .WithTags("Fee Approval Config")
            .RequireAuthorization();
        tiers.MapGet("/", GetTiers);
        tiers.MapPost("/", CreateTier);
        tiers.MapPut("/{id:guid}", UpdateTier);
        tiers.MapDelete("/{id:guid}", DeleteTier);

        // Appointment Approval Rule — off the fee-approval prefix (appointment is not a fee concern)
        var rule = app.MapGroup("/api/appointment-approval/rule")
            .WithTags("Appointment Approval Config")
            .RequireAuthorization();
        rule.MapGet("/", GetAppointmentRule);
        rule.MapPut("/", UpsertAppointmentRule);
    }

    // ─── Fee Approval Tiers ───────────────────────────────────────────────────

    private static async Task<IResult> GetTiers(WorkflowDbContext db)
    {
        var tiers = await db.FeeApprovalTiers
            .AsNoTracking()
            .OrderBy(t => t.Priority)
            .Select(t => new FeeApprovalTierDto(
                t.Id, t.MinAmount, t.MaxAmount,
                t.ApproverCode, t.AssignedType, t.TierLabel,
                t.Priority, t.IsActive, t.AppliesTo))
            .ToListAsync();
        return Results.Ok(tiers);
    }

    private static async Task<IResult> CreateTier(CreateFeeApprovalTierRequest req, WorkflowDbContext db)
    {
        var tier = FeeApprovalTier.Create(
            req.MinAmount, req.MaxAmount, req.ApproverCode,
            req.AssignedType, req.TierLabel, req.Priority, req.IsActive, req.AppliesTo);
        db.FeeApprovalTiers.Add(tier);
        await db.SaveChangesAsync();
        return Results.Created(
            $"/api/fee-approval/tiers/{tier.Id}",
            new FeeApprovalTierDto(tier.Id, tier.MinAmount, tier.MaxAmount,
                tier.ApproverCode, tier.AssignedType, tier.TierLabel,
                tier.Priority, tier.IsActive, tier.AppliesTo));
    }

    private static async Task<IResult> UpdateTier(Guid id, UpdateFeeApprovalTierRequest req, WorkflowDbContext db)
    {
        var tier = await db.FeeApprovalTiers.FindAsync(id);
        if (tier is null) return Results.NotFound();
        tier.Update(req.MinAmount, req.MaxAmount, req.ApproverCode,
            req.AssignedType, req.TierLabel, req.Priority, req.IsActive, req.AppliesTo);
        await db.SaveChangesAsync();
        return Results.Ok();
    }

    private static async Task<IResult> DeleteTier(Guid id, WorkflowDbContext db)
    {
        var tier = await db.FeeApprovalTiers.FindAsync(id);
        if (tier is null) return Results.NotFound();
        db.FeeApprovalTiers.Remove(tier);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // ─── Appointment Approval Rule ────────────────────────────────────────────

    private static async Task<IResult> GetAppointmentRule(WorkflowDbContext db)
    {
        var rule = await db.AppointmentApprovalRules
            .AsNoTracking()
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync();

        if (rule is null) return Results.NotFound();

        return Results.Ok(new AppointmentApprovalRuleDto(
            rule.Id, rule.WeekendHolidayEnabled, rule.WeekdayEnabled, rule.LeadTimeEnabled,
            rule.LeadTimeDays, rule.RescheduleEnabled, rule.RescheduleThreshold, rule.AppliesTo));
    }

    private static async Task<IResult> UpsertAppointmentRule(
        UpdateAppointmentApprovalRuleRequest req, WorkflowDbContext db)
    {
        var existing = await db.AppointmentApprovalRules
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            existing.Update(req.WeekendHolidayEnabled, req.WeekdayEnabled, req.LeadTimeEnabled,
                req.LeadTimeDays, req.RescheduleEnabled, req.RescheduleThreshold, req.AppliesTo);
        }
        else
        {
            var rule = AppointmentApprovalRule.Create(
                req.WeekendHolidayEnabled, req.WeekdayEnabled, req.LeadTimeEnabled,
                req.LeadTimeDays, req.RescheduleEnabled, req.RescheduleThreshold, req.AppliesTo);
            db.AppointmentApprovalRules.Add(rule);
        }

        await db.SaveChangesAsync();
        return Results.Ok();
    }
}
