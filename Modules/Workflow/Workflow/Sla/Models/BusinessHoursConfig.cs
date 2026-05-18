using Shared.DDD;

namespace Workflow.Sla.Models;

public class BusinessHoursConfig : Entity<Guid>
{
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public TimeOnly? LunchStartTime { get; private set; }
    public TimeOnly? LunchEndTime { get; private set; }
    public string TimeZone { get; private set; } = default!;
    public bool IsActive { get; private set; }

    private BusinessHoursConfig() { }

    public static BusinessHoursConfig Create(
        TimeOnly startTime,
        TimeOnly endTime,
        string timeZone,
        TimeOnly? lunchStartTime = null,
        TimeOnly? lunchEndTime = null)
    {
        ValidateTimeZone(timeZone);
        ValidateLunch(lunchStartTime, lunchEndTime, startTime, endTime);

        return new BusinessHoursConfig
        {
            Id = Guid.CreateVersion7(),
            StartTime = startTime,
            EndTime = endTime,
            LunchStartTime = lunchStartTime,
            LunchEndTime = lunchEndTime,
            TimeZone = timeZone,
            IsActive = true
        };
    }

    public void Update(
        TimeOnly startTime,
        TimeOnly endTime,
        string timeZone,
        bool isActive,
        TimeOnly? lunchStartTime = null,
        TimeOnly? lunchEndTime = null)
    {
        ValidateTimeZone(timeZone);
        ValidateLunch(lunchStartTime, lunchEndTime, startTime, endTime);

        StartTime = startTime;
        EndTime = endTime;
        LunchStartTime = lunchStartTime;
        LunchEndTime = lunchEndTime;
        TimeZone = timeZone;
        IsActive = isActive;
    }

    private static void ValidateTimeZone(string timeZone)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(timeZone); }
        catch (TimeZoneNotFoundException ex)
        {
            throw new ArgumentException(
                $"Time zone '{timeZone}' is not available on this host. " +
                "Install ICU/tzdata or pick a host-recognized zone.", nameof(timeZone), ex);
        }
    }

    private static void ValidateLunch(
        TimeOnly? lunchStart, TimeOnly? lunchEnd,
        TimeOnly dayStart, TimeOnly dayEnd)
    {
        var hasStart = lunchStart.HasValue;
        var hasEnd = lunchEnd.HasValue;

        if (hasStart != hasEnd)
            throw new ArgumentException(
                "LunchStartTime and LunchEndTime must both be set or both be null.");

        if (!hasStart) return;

        if (lunchStart!.Value >= lunchEnd!.Value)
            throw new ArgumentException(
                "LunchStartTime must be earlier than LunchEndTime.");

        if (lunchStart.Value < dayStart || lunchEnd.Value > dayEnd)
            throw new ArgumentException(
                "Lunch window must lie within the business hours window [StartTime, EndTime].");
    }
}
