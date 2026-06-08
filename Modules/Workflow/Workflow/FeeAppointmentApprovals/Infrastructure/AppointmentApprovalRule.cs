using Shared.DDD;
using Workflow.Contracts.FeeAppointmentApprovals;

namespace Workflow.FeeAppointmentApprovals.Infrastructure;

/// <summary>
/// Single-row configuration for appointment approval rules.
/// Stores the enabled conditions and thresholds for triggering appointment approval.
/// </summary>
public class AppointmentApprovalRule : Entity<Guid>
{
    /// <summary>Require approval if the new appointment date falls on a weekend or bank holiday.</summary>
    public bool WeekendHolidayEnabled { get; private set; }

    /// <summary>Require approval if the new appointment date falls on a normal weekday (not a weekend or holiday). Default off.</summary>
    public bool WeekdayEnabled { get; private set; }

    /// <summary>Require approval if the new appointment is less than LeadTimeDays away.</summary>
    public bool LeadTimeEnabled { get; private set; }

    /// <summary>Minimum lead-time days. Only evaluated when LeadTimeEnabled is true.</summary>
    public int? LeadTimeDays { get; private set; }

    /// <summary>Require approval once the reschedule count reaches RescheduleThreshold.</summary>
    public bool RescheduleEnabled { get; private set; }

    /// <summary>Number of reschedules (inclusive) that triggers approval. Only evaluated when RescheduleEnabled is true.</summary>
    public int? RescheduleThreshold { get; private set; }

    /// <summary>"Ext", "Int", or "Both"</summary>
    public string AppliesTo { get; private set; } = FeeApprovalRequestSource.External;

    private AppointmentApprovalRule() { }

    public static AppointmentApprovalRule Create(
        bool weekendHolidayEnabled,
        bool weekdayEnabled,
        bool leadTimeEnabled,
        int? leadTimeDays,
        bool rescheduleEnabled,
        int? rescheduleThreshold,
        string appliesTo = FeeApprovalRequestSource.External)
    {
        return new AppointmentApprovalRule
        {
            Id = Guid.CreateVersion7(),
            WeekendHolidayEnabled = weekendHolidayEnabled,
            WeekdayEnabled = weekdayEnabled,
            LeadTimeEnabled = leadTimeEnabled,
            LeadTimeDays = leadTimeDays,
            RescheduleEnabled = rescheduleEnabled,
            RescheduleThreshold = rescheduleThreshold,
            AppliesTo = appliesTo
        };
    }

    public void Update(
        bool weekendHolidayEnabled,
        bool weekdayEnabled,
        bool leadTimeEnabled,
        int? leadTimeDays,
        bool rescheduleEnabled,
        int? rescheduleThreshold,
        string appliesTo)
    {
        WeekendHolidayEnabled = weekendHolidayEnabled;
        WeekdayEnabled = weekdayEnabled;
        LeadTimeEnabled = leadTimeEnabled;
        LeadTimeDays = leadTimeDays;
        RescheduleEnabled = rescheduleEnabled;
        RescheduleThreshold = rescheduleThreshold;
        AppliesTo = appliesTo;
    }
}
