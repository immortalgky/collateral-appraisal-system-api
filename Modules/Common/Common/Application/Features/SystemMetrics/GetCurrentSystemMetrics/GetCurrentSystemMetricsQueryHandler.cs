using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Time;

namespace Common.Application.Features.SystemMetrics.GetCurrentSystemMetrics;

public class GetCurrentSystemMetricsQueryHandler(ISqlConnectionFactory connectionFactory, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetCurrentSystemMetricsQuery, IReadOnlyList<SystemMetricsCurrentDto>>
{
    private const int AliveWindowMinutes = 10;
    private const int Gen2WindowMinutes = 60;

    public async Task<IReadOnlyList<SystemMetricsCurrentDto>> Handle(
        GetCurrentSystemMetricsQuery query, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.ApplicationNow;

        const string sql = @"
;WITH Latest AS (
    SELECT *, ROW_NUMBER() OVER (PARTITION BY MachineName ORDER BY TimeStamp DESC) AS Rn
    FROM common.SystemMetricSamples
    WHERE TimeStamp >= @Cutoff
)
SELECT
    l.MachineName,
    l.TimeStamp,
    l.ProcessStartedAt,
    l.CpuPercent,
    l.MachineMemoryPercent,
    l.WorkingSetMb,
    l.GcHeapMb,
    CAST(COALESCE(
        (SELECT SUM(g.Gen2Collections) FROM common.SystemMetricSamples g
         WHERE g.MachineName = l.MachineName AND g.TimeStamp >= @Gen2WindowStart), 0) AS int) AS Gen2PerHour,
    l.ThreadCount,
    l.ThreadPoolQueue,
    l.RequestsPerMin,
    l.Http5xxPerMin,
    l.P95Ms,
    l.ExceptionsPerMin
FROM Latest l
WHERE l.Rn = 1
ORDER BY l.MachineName;";

        var parameters = new
        {
            Cutoff = now.AddMinutes(-AliveWindowMinutes),
            Gen2WindowStart = now.AddMinutes(-Gen2WindowMinutes)
        };

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<SystemMetricsCurrentDto>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        return rows.ToList();
    }
}
