using Shared.Exceptions;
using Shared.Time;

namespace Common.Application.Features.Logs;

/// <summary>
/// Shared time-range defaulting/validation for SearchLogs and GetLogSummary — both filter the
/// same way and must stay in sync (e.g. the 31-day span cap matches dbo.Logs' 30-day retention).
/// </summary>
public static class LogFilterRange
{
    private const int MaxSpanDays = 31;
    private const int DefaultSpanHours = 24;

    public static (DateTime From, DateTime To) Resolve(DateTime? from, DateTime? to, DateTime applicationNow)
    {
        // Deliberately not treating a date-only "to" (midnight) as "end of that day": the FE always
        // sends full timestamps, and expanding midnight would break a legitimate zoom that's
        // supposed to end exactly at 00:00:00.
        // ApplicationNow is safe to compare against dbo.Logs.TimeStamp here: production runs with
        // TimeZone:ForceUtc=false on Thai server time, so ApplicationNow and the server-local
        // timestamps Serilog writes (convertToUtc:false) are the same clock, not two different ones.
        var resolvedTo = to ?? applicationNow;
        var resolvedFrom = from ?? resolvedTo.AddHours(-DefaultSpanHours);

        if (resolvedFrom > resolvedTo)
            throw new BadRequestException("'from' must be before 'to'.");

        if ((resolvedTo - resolvedFrom).TotalDays > MaxSpanDays)
            throw new BadRequestException($"Time range must not exceed {MaxSpanDays} days.");

        return (resolvedFrom, resolvedTo);
    }
}
