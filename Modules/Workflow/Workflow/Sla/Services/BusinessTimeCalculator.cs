using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Workflow.Data;

namespace Workflow.Sla.Services;

public class BusinessTimeCalculator(WorkflowDbContext dbContext, IMemoryCache cache) : Shared.Sla.IBusinessTimeCalculator
{
    public async Task<DateTime> AddBusinessHoursAsync(DateTime from, int hours, CancellationToken ct = default)
    {
        var config = await GetBusinessHoursConfigAsync(ct);
        if (config is null)
            return from.AddHours(hours); // fallback to calendar time

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

        var remainingHours = hours;
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
            var businessHoursInDay = (dayEnd - dayStart).TotalHours;

            // If before business hours, snap to start
            if (current < dayStart)
                current = dayStart;

            // If after business hours, move to next day
            if (current >= dayEnd)
            {
                current = current.Date.AddDays(1).Add(config.StartTime.ToTimeSpan());
                continue;
            }

            var availableHours = (dayEnd - current).TotalHours;

            if (remainingHours <= availableHours)
            {
                current = current.AddHours(remainingHours);
                remainingHours = 0;
            }
            else
            {
                remainingHours -= (int)Math.Floor(availableHours);
                current = current.Date.AddDays(1).Add(config.StartTime.ToTimeSpan());
            }
        }

        return TimeZoneInfo.ConvertTimeToUtc(current, tz);
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

            return new BusinessHoursConfigDto(config.StartTime, config.EndTime, config.TimeZone);
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
                var dayStart = current.Add(config.StartTime.ToTimeSpan());
                var dayEnd = current.Add(config.EndTime.ToTimeSpan());

                // Clamp the from/to to this day's business window
                var windowStart = current == localFrom.Date
                    ? (localFrom < dayStart ? dayStart : localFrom)
                    : dayStart;

                var windowEnd = current == localTo.Date
                    ? (localTo > dayEnd ? dayEnd : localTo)
                    : dayEnd;

                if (windowEnd > windowStart)
                    totalMinutes += (int)Math.Round((windowEnd - windowStart).TotalMinutes);
            }

            current = current.AddDays(1);
        }

        return totalMinutes;
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

    private record BusinessHoursConfigDto(TimeOnly StartTime, TimeOnly EndTime, string TimeZone);
}
