using Shared.Data.Seed;

namespace Workflow.FeeAppointmentApprovals.Infrastructure;

/// <summary>
/// Seeds default FeeApprovalTier and AppointmentApprovalRule rows.
/// Idempotent: skipped if any rows already exist.
///
/// Fee tiers (based on plan defaults):
///   - Tier 1: 0–3000 THB → IntAdmin group (Priority 1)
///   - Tier 2: 3000+ THB  → IntAppraisalChecker group (Priority 2 = strictest)
///
/// Appointment rule defaults:
///   - WeekendHoliday: ENABLED
///   - LeadTime: ENABLED, N = 2 days
///   - Reschedule: ENABLED, M = 2 (i.e. RescheduleCount >= 2 triggers approval)
///
/// NOTE: ApproverCode uses the role names "IntAdmin" and "IntAppraisalChecker" which
/// correspond to the ASP.NET Identity role names seeded in AuthDataSeed.cs.
/// AssignedType = "2" means group/role assignment.
/// The user should verify these codes match their TaskAssignmentConfiguration rows.
/// </summary>
public class FeeApprovalDefaultConfigSeeder(
    WorkflowDbContext context,
    ILogger<FeeApprovalDefaultConfigSeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    public async Task SeedAllAsync()
    {
        await SeedFeeApprovalTiersAsync();
        await SeedAppointmentApprovalRuleAsync();
    }

    private async Task SeedFeeApprovalTiersAsync()
    {
        if (await context.FeeApprovalTiers.AnyAsync())
        {
            logger.LogInformation("FeeApprovalTiers already seeded, skipping");
            return;
        }

        var tiers = new[]
        {
            // Tier 1: any fee amount 0 to 3000 → IntAdmin group (lower priority = more permissive approver)
            FeeApprovalTier.Create(
                minAmount: 0m,
                maxAmount: 3000m,
                approverCode: "IntAdmin",      // Role name from AuthDataSeed.IntAdminRoleName
                assignedType: "2",             // group assignment
                tierLabel: "IntAdmin",
                priority: 1,
                isActive: true,
                appliesTo: "Ext"),

            // Tier 2: any fee amount over 3000 → IntAppraisalChecker group (higher priority = strictest)
            FeeApprovalTier.Create(
                minAmount: 3000.01m,
                maxAmount: null,               // no upper bound
                approverCode: "IntAppraisalChecker",  // Role name from AuthDataSeed.IntAppraisalCheckerRoleName
                assignedType: "2",
                tierLabel: "IntAppraisalChecker",
                priority: 2,
                isActive: true,
                appliesTo: "Ext")
        };

        context.FeeApprovalTiers.AddRange(tiers);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} default FeeApprovalTier rows", tiers.Length);
    }

    private async Task SeedAppointmentApprovalRuleAsync()
    {
        if (await context.AppointmentApprovalRules.AnyAsync())
        {
            logger.LogInformation("AppointmentApprovalRules already seeded, skipping");
            return;
        }

        var rule = AppointmentApprovalRule.Create(
            weekendHolidayEnabled: true,
            weekdayEnabled: false,   // weekday changes do NOT need approval by default
            leadTimeEnabled: true,
            leadTimeDays: 2,
            rescheduleEnabled: true,
            rescheduleThreshold: 2,
            appliesTo: "Ext");

        context.AppointmentApprovalRules.Add(rule);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded default AppointmentApprovalRule");
    }
}
