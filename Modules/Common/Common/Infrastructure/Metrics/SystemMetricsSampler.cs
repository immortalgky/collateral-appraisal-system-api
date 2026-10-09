using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.ExceptionServices;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Time;

namespace Common.Infrastructure.Metrics;

/// <summary>
/// Every 60s, writes one common.SystemMetricSamples row for this instance, and — at most once an
/// hour — purges rows past the 30-day retention (see PurgeOldSamplesAsync). Request/response
/// counters come from a MeterListener attached to ASP.NET Core's own "Microsoft.AspNetCore.Hosting"
/// meter (no new package, no OpenTelemetry pipeline needed — that meter is emitted by the framework
/// regardless of whether anything is listening to it).
/// </summary>
public class SystemMetricsSampler(IServiceScopeFactory scopeFactory, ILogger<SystemMetricsSampler> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);
    private static readonly string MachineName = Environment.MachineName;

    // Retention purge for common.SystemMetricSamples lives here, not in Shared's LogsCleanupJob —
    // this table is Common's own, and every writer already runs in this loop once a minute, so a
    // single extra "has an hour passed?" check is simpler than a second Hangfire job in Shared just
    // for one DELETE. At most once/hour, not every sample, since 60 rows/machine/day never needs
    // more than that to stay under the 30-day cap.
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(1);
    private const int RetentionDays = 30;
    private const int PurgeBatchSize = 5000;
    private DateTime _lastPurgeAtUtc = DateTime.MinValue;

    // Process.StartTime is DateTimeKind.Local (server-local) — convert to UTC once here so it can
    // go through IDateTimeProvider.ToApplicationTime like every other stored timestamp.
    private static readonly DateTime ProcessStartedAtUtc = GetProcessStartedAtUtc();

    private readonly MeterListener _meterListener = new();

    // All of these are written from request threads (meter callbacks) and read/reset once a
    // minute from the sampler loop — hence Interlocked/ConcurrentBag rather than plain fields.
    private long _requestCount;
    private long _http5xxCount;
    private long _exceptionCount;
    private ConcurrentBag<double> _durationsSeconds = [];

    private TimeSpan _lastCpuTime = GetCurrentProcessorTime();
    private DateTime _lastCpuSampleUtc = DateTime.UtcNow;
    private int _lastGen2Count = GC.CollectionCount(2);

    // Keep the delegate reference so StopAsync/Dispose can unsubscribe the exact same instance —
    // AppDomain.CurrentDomain.FirstChanceException += (_, _) => ... would create a NEW delegate
    // each time, so -= with a fresh lambda would never actually remove it.
    private EventHandler<FirstChanceExceptionEventArgs>? _firstChanceExceptionHandler;

    // Process.GetCurrentProcess() returns a new disposable handle every call — never let one leak.
    private static DateTime GetProcessStartedAtUtc()
    {
        using var process = Process.GetCurrentProcess();
        return process.StartTime.ToUniversalTime();
    }

    private static TimeSpan GetCurrentProcessorTime()
    {
        using var process = Process.GetCurrentProcess();
        return process.TotalProcessorTime;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // AppDomain-wide, not per-request — fine, this process only ever hosts one app. Stored so
        // StopAsync/Dispose can unsubscribe this exact delegate instance.
        _firstChanceExceptionHandler = OnFirstChanceException;
        AppDomain.CurrentDomain.FirstChanceException += _firstChanceExceptionHandler;

        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name != "Microsoft.AspNetCore.Hosting") return;
            if (instrument.Name is "http.server.request.duration")
                listener.EnableMeasurementEvents(instrument);
        };
        _meterListener.SetMeasurementEventCallback<double>(OnRequestDuration);
        _meterListener.Start();

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        if (_firstChanceExceptionHandler is not null)
        {
            AppDomain.CurrentDomain.FirstChanceException -= _firstChanceExceptionHandler;
            _firstChanceExceptionHandler = null;
        }

        return base.StopAsync(cancellationToken);
    }

    private void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
    {
        // Cancellation is normal control flow (a client hanging up, a poll interval racing a
        // request, CancellationToken propagation) — not the "something is silently throwing"
        // signal ExceptionsPerMin exists to surface.
        if (e.Exception is OperationCanceledException) return;
        Interlocked.Increment(ref _exceptionCount);
    }

    // http.server.request.duration — Histogram<double>, unit seconds. Tags per the ASP.NET Core 9
    // built-in metrics reference: http.route (present "if available"), http.response.status_code
    // (int, present "if one was sent").
    private void OnRequestDuration(
        Instrument instrument, double measurementSeconds,
        ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        string? route = null;
        var statusCode = 0;
        foreach (var tag in tags)
        {
            if (tag.Key == "http.route") route = tag.Value as string;
            else if (tag.Key == "http.response.status_code" && tag.Value is int code) statusCode = code;
        }

        if (IsExcludedRoute(route)) return;

        Interlocked.Increment(ref _requestCount);
        if (statusCode >= 500) Interlocked.Increment(ref _http5xxCount);
        _durationsSeconds.Add(measurementSeconds);
    }

    // Route templates come back without a leading slash (e.g. "admin/logs"); tolerate either form.
    // Missing route = static files / unmatched requests, excluded per the plan (they'd skew p95).
    // admin/logs* and admin/system-metrics* are the log-viewer page's own polling requests
    // (SearchLogs, GetLogById, GetLogSummary, GetSystemMetrics, GetCurrentSystemMetrics) — excluded
    // so the monitoring page doesn't inflate RequestsPerMin/P95Ms by measuring itself.
    private static bool IsExcludedRoute(string? route)
    {
        if (string.IsNullOrEmpty(route)) return true;
        var normalized = route.TrimStart('/');
        return normalized.StartsWith("notificationHub", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("health", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("hangfire", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("admin/logs", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("admin/system-metrics", StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await SampleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown — SampleAsync was cancelled mid-flight by the host stopping this
                // service, not a real failure. Exit quietly instead of logging a Warning on every
                // restart/deploy.
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[SYSTEM-METRICS] Failed to write a sample, will retry next interval");
            }
        }
    }

    private async Task SampleAsync(CancellationToken cancellationToken)
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();

        var nowUtc = DateTime.UtcNow;
        var cpuTime = process.TotalProcessorTime;
        var wallElapsed = nowUtc - _lastCpuSampleUtc;
        var cpuPercent = wallElapsed > TimeSpan.Zero
            ? Math.Clamp(
                (cpuTime - _lastCpuTime).TotalMilliseconds / (wallElapsed.TotalMilliseconds * Environment.ProcessorCount) * 100,
                0, 100)
            : 0;
        _lastCpuTime = cpuTime;
        _lastCpuSampleUtc = nowUtc;

        var gcInfo = GC.GetGCMemoryInfo();
        var memoryPercent = gcInfo.TotalAvailableMemoryBytes > 0
            ? Math.Clamp((double)gcInfo.MemoryLoadBytes / gcInfo.TotalAvailableMemoryBytes * 100, 0, 100)
            : 0;

        var gen2Count = GC.CollectionCount(2);
        var gen2Delta = gen2Count - _lastGen2Count;
        _lastGen2Count = gen2Count;

        // Swap the bag out so the next minute's measurements can't land in a batch we're already
        // sorting/writing.
        var durations = Interlocked.Exchange(ref _durationsSeconds, []);
        var p95Ms = RequestDurationPercentile.P95Ms(durations);

        var requestsPerMin = (int)Interlocked.Exchange(ref _requestCount, 0);
        var http5xxPerMin = (int)Interlocked.Exchange(ref _http5xxCount, 0);
        var exceptionsPerMin = (int)Interlocked.Exchange(ref _exceptionCount, 0);

        using var scope = scopeFactory.CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var parameters = new
        {
            TimeStamp = dateTimeProvider.ApplicationNow,
            MachineName,
            ProcessStartedAt = dateTimeProvider.ToApplicationTime(ProcessStartedAtUtc),
            CpuPercent = Math.Round((decimal)cpuPercent, 1),
            MachineMemoryPercent = Math.Round((decimal)memoryPercent, 1),
            WorkingSetMb = (int)(process.WorkingSet64 / 1024 / 1024),
            GcHeapMb = (int)(gcInfo.HeapSizeBytes / 1024 / 1024),
            Gen2Collections = gen2Delta,
            ThreadCount = ThreadPool.ThreadCount,
            ThreadPoolQueue = (int)ThreadPool.PendingWorkItemCount,
            RequestsPerMin = requestsPerMin,
            Http5xxPerMin = http5xxPerMin,
            P95Ms = p95Ms,
            ExceptionsPerMin = exceptionsPerMin
        };

        const string sql = @"
INSERT INTO common.SystemMetricSamples
    (TimeStamp, MachineName, ProcessStartedAt, CpuPercent, MachineMemoryPercent, WorkingSetMb, GcHeapMb,
     Gen2Collections, ThreadCount, ThreadPoolQueue, RequestsPerMin, Http5xxPerMin, P95Ms, ExceptionsPerMin)
VALUES
    (@TimeStamp, @MachineName, @ProcessStartedAt, @CpuPercent, @MachineMemoryPercent, @WorkingSetMb, @GcHeapMb,
     @Gen2Collections, @ThreadCount, @ThreadPoolQueue, @RequestsPerMin, @Http5xxPerMin, @P95Ms, @ExceptionsPerMin)";

        var connection = connectionFactory.GetOpenConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        if (nowUtc - _lastPurgeAtUtc >= PurgeInterval)
        {
            _lastPurgeAtUtc = nowUtc;
            await PurgeOldSamplesAsync(connection, dateTimeProvider.ApplicationNow, cancellationToken);
        }
    }

    // Errors here are swallowed (logged as Warning, not rethrown) — a missed purge just means the
    // table is briefly larger than 30 days' worth; it must never take down the sample write above,
    // which already succeeded by the time this runs.
    private async Task PurgeOldSamplesAsync(IDbConnection connection, DateTime applicationNow, CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = applicationNow.AddDays(-RetentionDays);
            var deleted = await connection.ExecuteAsync(new CommandDefinition(
                "DELETE TOP(@PurgeBatchSize) FROM common.SystemMetricSamples WHERE TimeStamp < @Cutoff",
                new { PurgeBatchSize, Cutoff = cutoff },
                cancellationToken: cancellationToken));

            if (deleted > 0)
                logger.LogInformation("[SYSTEM-METRICS] Purged {Count} sample rows older than {Cutoff}", deleted, cutoff);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[SYSTEM-METRICS] Failed to purge old samples, will retry next hour");
        }
    }

    public override void Dispose()
    {
        // Safety net in case the host tears down without calling StopAsync (e.g. an abrupt
        // shutdown) — unsubscribing twice is a harmless no-op.
        if (_firstChanceExceptionHandler is not null)
        {
            AppDomain.CurrentDomain.FirstChanceException -= _firstChanceExceptionHandler;
            _firstChanceExceptionHandler = null;
        }

        _meterListener.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
