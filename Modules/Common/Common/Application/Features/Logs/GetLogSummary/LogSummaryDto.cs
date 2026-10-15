namespace Common.Application.Features.Logs.GetLogSummary;

public record LogLevelCounts(long Information, long Warning, long Error, long Fatal);

public record LogHistogramBucket(DateTime Start, long Information, long Warning, long Error);

public record LogTopProblem(
    string Template, string Level, long Count, DateTime LastSeen, string? SampleMessage,
    string? SourceContext, string? ExceptionType);

public record LogSummaryDto(
    LogLevelCounts LevelCounts,
    int BucketSeconds,
    IReadOnlyList<LogHistogramBucket> Buckets,
    IReadOnlyList<LogTopProblem> TopProblems
);
