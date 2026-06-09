using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Workflow.Contracts.Sla;
using Workflow.Data;

namespace Workflow.Sla.Services;

public class BusinessTimeCalculator(WorkflowDbContext dbContext, IMemoryCache cache) : IBusinessTimeCalculator
{
    public async Task<DateTime> AddBusinessHoursAsync(DateTime from, int hours, CancellationToken ct = default)
    {
        var config = await GetBusinessHoursConfigAsync(ct);
        if (config is null)
            // Fallback to plain calendar time when no business-hours config is seeded. Surface the
            // result as Unspecified-local to match the main return path (all current callers pass a
            // local `from`), so the method's output Kind is consistent regardless of config presence.
            return DateTime.SpecifyKind(from.AddHours(hours), DateTimeKind.Unspecified);

        var tz = TimeZoneInfo.FindSystemTimeZoneById(config.TimeZone);
        var holidays = await GetHolidaysAsync(from, hours, ct);

        // DateTimeKind.Utc is converted from UTC explicitly.
        // DateTimeKind.Unspecified is treated as application wall-clock time (the configured timezone),
        // so we use the 3-arg ConvertTime overload with sourceTZ = configTZ to avoid interpreting
        // Unspecified as the server's local timezone (which may differ in container environments).
        // DateTimeKind.Local is treated the same as Unspecified for safety.
        var localFrom = from.Kind == DateTimeKind.Utc
            ? TimeZoneInfo.ConvertTimeFromUtc(from, tz)
            : TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(from, DateTimeKind.Unspecified), tz, tz);

        var remainingHours = (double)hours;
        var current = localFrom;

        while (remainingHours > 0)
        {
            // Skip weekends and holidays
            if (current.DayOfWeek == DayOfWeek.Saturday || current.DayOfWeek == DayOfWeek.Sunday ||
                holidays.Contains(DateOnly.FromDateTime(current)))
            {
                current = current.Date.AddDays(1).Add(config.StartTime.ToTimeSpan());
                continue;
            }

            var dayStart = current.Date.Add(config.StartTime.ToTimeSpan());
            var dayEnd = current.Date.Add(config.EndTime.ToTimeSpan());

            // If before business hours, snap to start
            if (current < dayStart)
                current = dayStart;

            // If after business hours, move to next day
            if (current >= dayEnd)
            {
                current = current.Date.AddDays(1).Add(config.StartTime.ToTimeSpan());
                continue;
            }

            // Snap cursor out of lunch break
            if (config.LunchStartTime.HasValue && config.LunchEndTime.HasValue)
            {
                var lunchStart = current.Date.Add(config.LunchStartTime.Value.ToTimeSpan());
                var lunchEnd = current.Date.Add(config.LunchEndTime.Value.ToTimeSpan());
                if (current >= lunchStart && current < lunchEnd)
                    current = lunchEnd;
            }

            // Re-check after potential lunch snap: cursor may now be at or past dayEnd
            if (current >= dayEnd)
            {
                current = current.Date.AddDays(1).Add(config.StartTime.ToTimeSpan());
                continue;
            }

            var sessions = GetDaySessions(config, current.Date);

            foreach (var (sessionStart, sessionEnd) in sessions)
            {
                if (current >= sessionEnd) continue; // already past this session

                var effectiveStart = current < sessionStart ? sessionStart : current;
                var available = (sessionEnd - effectiveStart).TotalHours;

                if (available <= 0) continue;

                if (remainingHours <= available)
                {
                    current = effectiveStart.AddHours(remainingHours);
                    remainingHours = 0;
                    break;
                }

                remainingHours -= available;
                current = sessionEnd;
            }

            if (remainingHours > 0)
            {
                // Exhausted all sessions for this day — move to next day
                current = current.Date.AddDays(1).Add(config.StartTime.ToTimeSpan());
            }
        }

        // Return the cursor as application-local wall-clock (the configured business timezone),
        // NOT UTC. The whole app persists and compares dates in local time (IDateTimeProvider.
        // ApplicationNow, GETDATE() in views, SlaMonitorService compares against ApplicationNow),
        // so returning local keeps SLA due dates consistent with CreatedAt and removes the need for
        // per-caller UTC→local conversion. `current` is already wall-clock in `tz`; surface it as
        // Unspecified to match the Kind produced by IDateTimeProvider.ApplicationNow.
        return DateTime.SpecifyKind(current, DateTimeKind.Unspecified);
    }

    private async Task<BusinessHoursConfigDto?> GetBusinessHoursConfigAsync(CancellationToken ct)
    {
        return await cache.GetOrCreateAsync("sla:business-hours", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            var config = await dbContext.BusinessHoursConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.IsActive, ct);

            if (config is null) return null;

            return new BusinessHoursConfigDto(
                config.StartTime, config.EndTime, config.TimeZone,
                config.LunchStartTime, config.LunchEndTime);
        });
    }

    public async Task<int> GetBusinessMinutesBetweenAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (from >= to) return 0;

        var config = await GetBusinessHoursConfigAsync(ct);
        if (config is null)
            return (int)(to - from).TotalMinutes; // fallback: calendar minutes

        var tz = TimeZoneInfo.FindSystemTimeZoneById(config.TimeZone);

        // Unspecified/Local → treat as application wall-clock (configured TZ) using 3-arg ConvertTime
        // so that server TZ (UTC in containers) does not silently shift the result.
        // Utc → convert from UTC explicitly.
        var localFrom = from.Kind == DateTimeKind.Utc
            ? TimeZoneInfo.ConvertTimeFromUtc(from, tz)
            : TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(from, DateTimeKind.Unspecified), tz, tz);
        var localTo = to.Kind == DateTimeKind.Utc
            ? TimeZoneInfo.ConvertTimeFromUtc(to, tz)
            : TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(to, DateTimeKind.Unspecified), tz, tz);

        var startDate = DateOnly.FromDateTime(localFrom);
        var endDate = DateOnly.FromDateTime(localTo);
        var holidays = await GetHolidaysByRangeAsync(startDate, endDate, ct);

        var totalMinutes = 0;
        var current = localFrom.Date;

        while (current <= localTo.Date)
        {
            var currentDate = DateOnly.FromDateTime(current);

            if (current.DayOfWeek != DayOfWeek.Saturday
                && current.DayOfWeek != DayOfWeek.Sunday
                && !holidays.Contains(currentDate))
            {
                var sessions = GetDaySessions(config, current);

                foreach (var (sessionStart, sessionEnd) in sessions)
                {
                    var windowStart = current == localFrom.Date
                        ? (localFrom < sessionStart ? sessionStart : localFrom)
                        : sessionStart;

                    var windowEnd = current == localTo.Date
                        ? (localTo > sessionEnd ? sessionEnd : localTo)
                        : sessionEnd;

                    if (windowEnd > windowStart)
                        totalMinutes += (int)Math.Round((windowEnd - windowStart).TotalMinutes);
                }
            }

            current = current.AddDays(1);
        }

        return totalMinutes;
    }

    private static IEnumerable<(DateTime Start, DateTime End)> GetDaySessions(
        BusinessHoursConfigDto config, DateTime day)
    {
        var dayStart = day.Date.Add(config.StartTime.ToTimeSpan());
        var dayEnd = day.Date.Add(config.EndTime.ToTimeSpan());

        if (config.LunchStartTime.HasValue && config.LunchEndTime.HasValue)
        {
            var lunchStart = day.Date.Add(config.LunchStartTime.Value.ToTimeSpan());
            var lunchEnd = day.Date.Add(config.LunchEndTime.Value.ToTimeSpan());
            yield return (dayStart, lunchStart);
            yield return (lunchEnd, dayEnd);
        }
        else
        {
            yield return (dayStart, dayEnd);
        }
    }

    private async Task<HashSet<DateOnly>> GetHolidaysAsync(DateTime from, int hours, CancellationToken ct)
    {
        // Estimate max days needed (worst case: 1 business hour per day)
        var maxDays = hours * 2;
        var startDate = DateOnly.FromDateTime(from);
        var endDate = startDate.AddDays(maxDays);
        return await GetHolidaysByRangeAsync(startDate, endDate, ct);
    }

    private async Task<HashSet<DateOnly>> GetHolidaysByRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct)
    {
        var cacheKey = $"sla:holidays:{startDate.Year}-{endDate.Year}";
        return await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var holidays = await dbContext.Holidays
                .AsNoTracking()
                .Where(h => h.Year >= startDate.Year && h.Year <= endDate.Year)
                .Select(h => h.Date)
                .ToListAsync(ct);

            return holidays.ToHashSet();
        }) ?? [];
    }

    private record BusinessHoursConfigDto(
        TimeOnly StartTime,
        TimeOnly EndTime,
        string TimeZone,
        TimeOnly? LunchStartTime,
        TimeOnly? LunchEndTime);
}
