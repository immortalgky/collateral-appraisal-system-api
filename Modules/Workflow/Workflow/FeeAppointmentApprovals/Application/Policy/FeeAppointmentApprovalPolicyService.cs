using Workflow.FeeAppointmentApprovals.Infrastructure;

namespace Workflow.FeeAppointmentApprovals.Application.Policy;

/// <summary>
/// Implements the approval policy by reading AppointmentApprovalRule and FeeApprovalTier
/// from the WorkflowDbContext and applying the configured conditions.
/// Reuses BusinessTimeCalculator (via IBusinessTimeCalculator) for weekend/holiday detection.
/// </summary>
public class FeeAppointmentApprovalPolicyService(
    WorkflowDbContext dbContext,
    ILogger<FeeAppointmentApprovalPolicyService> logger)
    : IFeeAppointmentApprovalPolicyService
{
    public async Task<bool> RequiresAppointmentApprovalAsync(
        DateTime newDate,
        int rescheduleCount,
        string requestSource,
        CancellationToken ct = default)
    {
        var rule = await dbContext.AppointmentApprovalRules
            .AsNoTracking()
            .Where(r => r.AppliesTo == "Both" || r.AppliesTo == requestSource)
            .OrderByDescending(r => r.Id) // take the most recently inserted active rule
            .FirstOrDefaultAsync(ct);

        if (rule is null)
        {
            logger.LogWarning("No AppointmentApprovalRule found for source '{Source}'; defaulting to no approval required", requestSource);
            return false;
        }

        // Day-of-week rules. Resolve weekend/holiday once if either day-based rule is enabled.
        if (rule.WeekendHolidayEnabled || rule.WeekdayEnabled)
        {
            var isWeekend = newDate.DayOfWeek == DayOfWeek.Saturday || newDate.DayOfWeek == DayOfWeek.Sunday;
            var dateOnly = DateOnly.FromDateTime(newDate);
            var isHoliday = await dbContext.Holidays
                .AsNoTracking()
                .AnyAsync(h => h.Date == dateOnly, ct);

            // Rule 1: Weekend or holiday
            if (rule.WeekendHolidayEnabled && (isWeekend || isHoliday))
            {
                logger.LogDebug("Appointment requires approval: new date {Date} is a weekend/holiday", newDate);
                return true;
            }

            // Rule 1b: Normal weekday (a business day — not a weekend and not a holiday)
            if (rule.WeekdayEnabled && !isWeekend && !isHoliday)
            {
                logger.LogDebug("Appointment requires approval: new date {Date} is a weekday", newDate);
                return true;
            }
        }

        // Rule 2: Lead time too short
        if (rule.LeadTimeEnabled && rule.LeadTimeDays.HasValue)
        {
            var leadDays = (newDate.Date - DateTime.Now.Date).TotalDays;
            if (leadDays < rule.LeadTimeDays.Value)
            {
                logger.LogDebug("Appointment requires approval: lead time {Days} days < minimum {Min}", leadDays, rule.LeadTimeDays.Value);
                return true;
            }
        }

        // Rule 3: Too many reschedules
        if (rule.RescheduleEnabled && rule.RescheduleThreshold.HasValue)
        {
            if (rescheduleCount >= rule.RescheduleThreshold.Value)
            {
                logger.LogDebug("Appointment requires approval: reschedule count {Count} >= threshold {Threshold}", rescheduleCount, rule.RescheduleThreshold.Value);
                return true;
            }
        }

        return false;
    }

    public async Task<FeeApprovalTierMatch?> GetFeeTierAsync(
        decimal totalFeeAmount,
        string requestSource,
        CancellationToken ct = default)
    {
        if (totalFeeAmount <= 0) return null;

        var tiers = await dbContext.FeeApprovalTiers
            .AsNoTracking()
            .Where(t => t.IsActive
                        && (t.AppliesTo == "Both" || t.AppliesTo == requestSource)
                        && t.MinAmount <= totalFeeAmount
                        && (t.MaxAmount == null || t.MaxAmount >= totalFeeAmount))
            .OrderBy(t => t.Priority)
            .ToListAsync(ct);

        var tier = tiers.FirstOrDefault();
        if (tier is null) return null;

        return new FeeApprovalTierMatch(tier.Id, tier.ApproverCode, tier.AssignedType, tier.Priority, tier.TierLabel);
    }

    public async Task<FeeApprovalTierMatch> GetLowestActiveTierAsync(
        string requestSource,
        CancellationToken ct = default)
    {
        var tier = await dbContext.FeeApprovalTiers
            .AsNoTracking()
            .Where(t => t.IsActive && (t.AppliesTo == "Both" || t.AppliesTo == requestSource))
            .OrderBy(t => t.Priority)
            .FirstOrDefaultAsync(ct);

        if (tier is not null)
            return new FeeApprovalTierMatch(tier.Id, tier.ApproverCode, tier.AssignedType, tier.Priority, tier.TierLabel);

        // Hardcoded fallback: IntAdmin group (matches the seeded default)
        logger.LogWarning("No active FeeApprovalTier found for source '{Source}'; using hardcoded IntAdmin fallback", requestSource);
        return new FeeApprovalTierMatch(Guid.Empty, "IntAdmin", "2", 1, "IntAdmin");
    }

    public FeeApprovalTierMatch? PickStrictestTier(FeeApprovalTierMatch? appointmentTier, FeeApprovalTierMatch? feeTier)
    {
        if (appointmentTier is null && feeTier is null) return null;
        if (appointmentTier is null) return feeTier;
        if (feeTier is null) return appointmentTier;

        // Higher Priority number = stricter tier
        return appointmentTier.Priority >= feeTier.Priority ? appointmentTier : feeTier;
    }
}
