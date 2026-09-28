using Common.Application.Features.Logs;
using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Time;

namespace Common.Application.Features.SystemMetrics.GetSystemMetrics;

public class GetSystemMetricsQueryHandler(ISqlConnectionFactory connectionFactory, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetSystemMetricsQuery, SystemMetricsDto>
{
    private const int DefaultBuckets = 60;
    private const int MaxBuckets = 200;

    public async Task<SystemMetricsDto> Handle(GetSystemMetricsQuery query, CancellationToken cancellationToken)
    {
        var filter = query.Filter;

        // Same defaulting/validation as the log summary (last 24h, 31-day span cap).
        var (from, to) = LogFilterRange.Resolve(filter.From, filter.To, dateTimeProvider.ApplicationNow);
        var bucketCount = Math.Clamp(filter.Buckets ?? DefaultBuckets, 1, MaxBuckets);
        var bucketSeconds = Math.Max(1, (int)Math.Ceiling((to - from).TotalSeconds / bucketCount));

        var parameters = new { From = from, To = to, BucketSeconds = bucketSeconds, BucketCount = bucketCount };

        // The CASE clamps the boundary row at TimeStamp == @To (which divides out to exactly
        // @BucketCount) into the last bucket instead of a phantom 49th group, so AVG stays a single
        // correctly-weighted aggregate per bucket rather than something to merge client-side.
        const string sql = @"
SELECT
    MachineName,
    CASE WHEN DATEDIFF(SECOND, @From, TimeStamp) / @BucketSeconds >= @BucketCount THEN @BucketCount - 1
         ELSE DATEDIFF(SECOND, @From, TimeStamp) / @BucketSeconds END AS BucketIndex,
    CAST(AVG(CAST(CpuPercent AS float)) AS float) AS CpuPercent,
    CAST(AVG(CAST(MachineMemoryPercent AS float)) AS float) AS MachineMemoryPercent,
    CAST(AVG(CAST(RequestsPerMin AS float)) AS float) AS RequestsPerMin,
    CAST(MAX(ThreadPoolQueue) AS int) AS ThreadPoolQueue,
    CAST(MAX(P95Ms) AS int) AS P95Ms,
    CAST(MAX(Http5xxPerMin) AS int) AS Http5xxPerMin
FROM common.SystemMetricSamples
WHERE TimeStamp >= @From AND TimeStamp <= @To
GROUP BY MachineName,
    CASE WHEN DATEDIFF(SECOND, @From, TimeStamp) / @BucketSeconds >= @BucketCount THEN @BucketCount - 1
         ELSE DATEDIFF(SECOND, @From, TimeStamp) / @BucketSeconds END;

SELECT DISTINCT MachineName, ProcessStartedAt AS At
FROM common.SystemMetricSamples
WHERE TimeStamp >= @From AND TimeStamp <= @To
  AND ProcessStartedAt >= @From AND ProcessStartedAt <= @To
ORDER BY MachineName, At;";

        var connection = connectionFactory.GetOpenConnection();
        using var grid = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        var bucketRows = (await grid.ReadAsync<BucketRow>()).ToList();
        var restarts = (await grid.ReadAsync<SystemMetricsRestart>()).ToList();

        var machines = bucketRows
            .GroupBy(r => r.MachineName)
            .Select(g => new SystemMetricsMachine(g.Key, BuildPoints(g.ToList(), from, bucketSeconds, bucketCount)))
            .OrderBy(m => m.MachineName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SystemMetricsDto(bucketSeconds, machines, restarts);
    }

    private static List<SystemMetricsPoint> BuildPoints(
        List<BucketRow> rows, DateTime from, int bucketSeconds, int bucketCount)
    {
        var byIndex = rows.ToDictionary(r => r.BucketIndex);
        var points = new List<SystemMetricsPoint>(bucketCount);
        for (var i = 0; i < bucketCount; i++)
        {
            byIndex.TryGetValue(i, out var row);
            points.Add(new SystemMetricsPoint(
                from.AddSeconds(i * (double)bucketSeconds),
                row?.CpuPercent, row?.MachineMemoryPercent, row?.ThreadPoolQueue, row?.P95Ms,
                row?.RequestsPerMin, row?.Http5xxPerMin));
        }

        return points;
    }

    private record BucketRow(
        string MachineName,
        int BucketIndex,
        double? CpuPercent,
        double? MachineMemoryPercent,
        double? RequestsPerMin,
        int? ThreadPoolQueue,
        int? P95Ms,
        int? Http5xxPerMin
    );
}
