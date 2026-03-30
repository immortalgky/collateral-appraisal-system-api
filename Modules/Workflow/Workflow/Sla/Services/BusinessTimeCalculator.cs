using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Workflow.Data;

namespace Workflow.Sla.Services;

public class BusinessTimeCalculator(WorkflowDbContext dbContext, IMemoryCache cache) : IBusinessTimeCalculator
{
    public async Task<DateTime> AddBusinessHoursAsync(DateTime from, int hours, CancellationToken ct = default)
    {
        var config = await GetBusinessHoursConfigAsync(ct);
        if (config is null)
            return from.AddHours(hours); // fallback to calendar time

        var tz = TimeZoneInfo.FindSystemTimeZoneById(config.TimeZone);
        var holidays = await GetHolidaysAsync(from, hours, ct);

        var localFrom = TimeZoneInfo.ConvertTimeFromUtc(
            from.Kind == DateTimeKind.Utc ? from : DateTime.SpecifyKind(from, DateTimeKind.Utc), tz);

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

    private async Task<HashSet<DateOnly>> GetHolidaysAsync(DateTime from, int hours, CancellationToken ct)
    {
        // Estimate max days needed (worst case: 1 business hour per day)
        var maxDays = hours * 2;
        var startDate = DateOnly.FromDateTime(from);
        var endDate = startDate.AddDays(maxDays);

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
