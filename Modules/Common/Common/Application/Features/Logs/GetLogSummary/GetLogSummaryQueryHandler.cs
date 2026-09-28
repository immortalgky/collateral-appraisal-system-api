using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Time;

namespace Common.Application.Features.Logs.GetLogSummary;

public class GetLogSummaryQueryHandler(ISqlConnectionFactory connectionFactory, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetLogSummaryQuery, LogSummaryDto>
{
    private const int BucketCount = 48;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "Nothing user-supplied is interpolated. The WHERE clause comes from LogQueryParser/LogQueryContext, " +
            "which emit only fixed SQL fragments with every search value bound as a @parameter; " +
            "IdBoundsSql is built only from compile-time constants.")]
    public async Task<LogSummaryDto> Handle(GetLogSummaryQuery query, CancellationToken cancellationToken)
    {
        var filter = query.Filter;
        var applicationNow = dateTimeProvider.ApplicationNow;
        var ctx = LogQueryContext.Build(filter.Q, filter.From, filter.To, applicationNow);
        var (from, to) = (ctx.From, ctx.To);

        // At least 1 second so DATEDIFF/@BucketSeconds never divides by zero for a sub-second range.
        var bucketSeconds = Math.Max(1, (int)Math.Ceiling((to - from).TotalSeconds / BucketCount));

        var parameters = ctx.Parameters;
        parameters.Add("BucketSeconds", bucketSeconds);

        var where = string.Join(" AND ", ctx.Conditions);
        var idBoundsSql = ctx.IdBoundsSql;

        // levelCounts is derived from these same bucket rows (summed below) rather than a separate
        // GROUP BY Level query — that would be a third full scan of the window, and the window scan
        // is the expensive part once a free-text LIKE term is involved.
        //
        // Top problems also group by exception type, not just Template: CustomExceptionHandler logs
        // every exception through the same constant template ("An error occurred: {Message}"), so
        // grouping by Template alone lumps a NotFoundException in with a SqlException in with a
        // ValidationException under one bucket. ExceptionType pulls the leading "System.Foo.BarException"
        // off Exception's FIRST LINE only (everything before the first ':' on that line — .NET's
        // Exception.ToString() always starts "{FullTypeName}" alone, or "{FullTypeName}: {Message}"
        // when Message isn't empty, then a CRLF before the stack trace). Restricting to the first
        // line matters: an earlier version searched the whole (possibly multi-line) string for the
        // first ':' — when Message was empty, that colon search fell straight into the stack trace
        // and could pick up a Windows drive-letter colon (e.g. "in C:\...") as if it were part of the
        // type name. PATINDEX finds the first CR or LF to isolate the first line; NULL-safe throughout
        // (NULL for rows with no Exception, e.g. a plain LogWarning, same as today).
        var sql = $@"
{idBoundsSql}
SELECT
    DATEDIFF(SECOND, @From, TimeStamp) / @BucketSeconds AS BucketIndex,
    CAST(SUM(CASE WHEN Level = 'Information' THEN 1 ELSE 0 END) AS bigint) AS Information,
    CAST(SUM(CASE WHEN Level = 'Warning' THEN 1 ELSE 0 END) AS bigint) AS Warning,
    CAST(SUM(CASE WHEN Level = 'Error' THEN 1 ELSE 0 END) AS bigint) AS Error,
    CAST(SUM(CASE WHEN Level = 'Fatal' THEN 1 ELSE 0 END) AS bigint) AS Fatal
FROM dbo.Logs
WHERE {where}
GROUP BY DATEDIFF(SECOND, @From, TimeStamp) / @BucketSeconds;

SELECT TOP 6
    COALESCE(MessageTemplate, LEFT(Message, 200)) AS Template,
    CASE
        WHEN MAX(CASE WHEN Level = 'Fatal' THEN 1 ELSE 0 END) = 1 THEN 'Fatal'
        WHEN MAX(CASE WHEN Level = 'Error' THEN 1 ELSE 0 END) = 1 THEN 'Error'
        ELSE 'Warning'
    END AS Level,
    COUNT_BIG(*) AS [Count],
    MAX(TimeStamp) AS LastSeen,
    MIN(Message) AS SampleMessage,
    MAX(COALESCE(SourceContext, JSON_VALUE(Properties, '$.Properties.SourceContext'))) AS SourceContext,
    et.ExceptionType
FROM dbo.Logs
CROSS APPLY (SELECT PATINDEX('%[' + CHAR(13) + CHAR(10) + ']%', Exception) AS NlPos) nl
CROSS APPLY (SELECT LEFT(Exception, CASE WHEN nl.NlPos = 0 THEN LEN(Exception) ELSE nl.NlPos - 1 END) AS FirstLine) fl
CROSS APPLY (SELECT LEFT(fl.FirstLine, CHARINDEX(':', fl.FirstLine + ':') - 1) AS ExceptionType) et
WHERE {where} AND Level IN ('Warning', 'Error', 'Fatal')
GROUP BY COALESCE(MessageTemplate, LEFT(Message, 200)), et.ExceptionType
ORDER BY [Count] DESC;";

        var connection = connectionFactory.GetOpenConnection();
        using var grid = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        var bucketRows = (await grid.ReadAsync<BucketRow>()).ToList();
        var (buckets, levelCounts) = BuildBucketsAndLevelCounts(bucketRows, from, bucketSeconds);
        var topProblems = (await grid.ReadAsync<LogTopProblem>()).ToList();

        return new LogSummaryDto(levelCounts, bucketSeconds, buckets, topProblems);
    }

    private static (List<LogHistogramBucket> Buckets, LogLevelCounts LevelCounts) BuildBucketsAndLevelCounts(
        List<BucketRow> rows, DateTime from, int bucketSeconds)
    {
        var counts = new (long Information, long Warning, long Error, long Fatal)[BucketCount];
        foreach (var row in rows)
        {
            // The row at TimeStamp == @To lands exactly on index BucketCount due to integer
            // division rounding — clamp it into the last bucket rather than dropping it.
            var index = Math.Clamp(row.BucketIndex, 0, BucketCount - 1);
            var c = counts[index];
            counts[index] = (c.Information + row.Information, c.Warning + row.Warning,
                c.Error + row.Error, c.Fatal + row.Fatal);
        }

        var buckets = new List<LogHistogramBucket>(BucketCount);
        long totalInformation = 0, totalWarning = 0, totalError = 0, totalFatal = 0;
        for (var i = 0; i < BucketCount; i++)
        {
            var c = counts[i];
            totalInformation += c.Information;
            totalWarning += c.Warning;
            totalError += c.Error;
            totalFatal += c.Fatal;

            // Bucket's "error" folds Fatal in, per contract; levelCounts keeps them separate.
            buckets.Add(new LogHistogramBucket(from.AddSeconds(i * (double)bucketSeconds),
                c.Information, c.Warning, c.Error + c.Fatal));
        }

        var levelCounts = new LogLevelCounts(totalInformation, totalWarning, totalError, totalFatal);
        return (buckets, levelCounts);
    }

    private record BucketRow(int BucketIndex, long Information, long Warning, long Error, long Fatal);
}
