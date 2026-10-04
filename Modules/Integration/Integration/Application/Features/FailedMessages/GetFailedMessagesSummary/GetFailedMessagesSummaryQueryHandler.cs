using System.Data;
using System.Text.Json;
using Dapper;
using Integration.Application.Features.OutboxMessages;
using Integration.Domain.FailedMessages;
using Integration.FailedMessages;
using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;
using Shared.Time;

namespace Integration.Application.Features.FailedMessages.GetFailedMessagesSummary;

public class GetFailedMessagesSummaryQueryHandler(
    ISqlConnectionFactory sqlConnectionFactory,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetFailedMessagesSummaryQuery, FailedMessagesSummaryDto>
{
    private static readonly JsonSerializerOptions CamelCaseOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "outboxUnion is built by OutboxUnionSql.Build, which only ever interpolates its own six " +
            "hard-coded OutboxModuleWhitelist.Modules schema literals — never caller input (this query has " +
            "no module/status parameter from the request at all). Since/StuckThreshold are bound as " +
            "@parameters.")]
    public async Task<FailedMessagesSummaryDto> Handle(
        GetFailedMessagesSummaryQuery request, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.ApplicationNow;
        var since24h = now.AddHours(-24);
        // "Stuck" is OutboxUnionSql.StuckPredicate — the very predicate GET /admin/outbox-messages?status=Stuck uses.

        // OccurredAt is selected too — the Stuck-count CASE below needs it for the rolling-deploy
        // NULL-ProcessingStartedAt rule, not just Status/ProcessingStartedAt. Module/schema
        // names come only from OutboxUnionSql's own whitelist iteration (OutboxModuleWhitelist.Modules),
        // never caller input — the same rule every other outbox query in this feature already follows.
        // The summary only ever counts Failed/Processing rows, so the filter is
        // pushed inside each per-module branch — each one can then seek
        // IX_IntegrationEventOutbox_Polling (Status, OccurredAt) for just those statuses, instead of
        // scanning every Pending/Processed row in all six schemas only to discard them after the UNION.
        var outboxUnion = OutboxUnionSql.Build(
            "Status, ProcessingStartedAt, OccurredAt", "Status IN ('Failed', 'Processing')");

        // ONE round trip for everything — a multi-statement batch (Dapper
        // QueryMultipleAsync) instead of 6 separate ones, and the outbox Failed/Stuck counts share a
        // single UNION pass (SUM(CASE…)) instead of two. Every parameter is still bound, not interpolated.
        var sql = $"""
            SELECT COUNT(*) AS PendingCount, MIN(FaultedAt) AS OldestPendingAt
            FROM integration.FailedMessages
            WHERE Status = 'Pending';

            SELECT
                (SELECT COUNT(*) FROM integration.FailedMessages WHERE FaultedAt >= @Since) AS Total,
                (SELECT COUNT(*) FROM integration.FailedMessages WHERE Status = 'Retried' AND ActionAt >= @Since) AS Retried,
                (SELECT COUNT(*) FROM integration.FailedMessages WHERE Status = 'Discarded' AND ActionAt >= @Since) AS Discarded;

            SELECT TOP 5 SourceQueue AS Queue, ExceptionType, COUNT(*) AS Count
            FROM integration.FailedMessages
            WHERE Status = 'Pending'
            GROUP BY SourceQueue, ExceptionType
            ORDER BY COUNT(*) DESC;

            SELECT
                SUM(CASE WHEN o.Status = 'Failed' THEN 1 ELSE 0 END) AS FailedCount,
                SUM(CASE WHEN {OutboxUnionSql.StuckPredicate} THEN 1 ELSE 0 END) AS StuckCount
            FROM ({outboxUnion}) o;

            SELECT Node, CollectedAt, ManagementStatus, QueuesJson, LastError FROM integration.BrokerSnapshots;

            -- D2: Pending only — a row already RetryRequested is on its way out and no longer counts as
            -- an outstanding error/skip.
            SELECT Node, SourceQueue, Kind, COUNT(*) AS Count
            FROM integration.FailedMessages
            WHERE Status = 'Pending'
            GROUP BY Node, SourceQueue, Kind;
            """;

        // DateTime2, like the columns they are compared with: a plain DateTime binds as SqlDbType.DateTime
        // (3.33 ms rounding).
        var parameters = new DynamicParameters();
        parameters.Add("Since", since24h, DbType.DateTime2);
        parameters.AddStuckThreshold(now);

        var connection = sqlConnectionFactory.GetOpenConnection();
        await using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        var pending = await multi.ReadFirstOrDefaultAsync<PendingRow>();
        var last24h = await multi.ReadFirstOrDefaultAsync<Last24hDto>();
        var topGroups = (await multi.ReadAsync<TopGroupDto>()).ToList();
        var outboxCounts = await multi.ReadFirstOrDefaultAsync<OutboxCountsRow>();
        var snapshots = (await multi.ReadAsync<BrokerSnapshotRow>()).ToList();
        var queueCounts = (await multi.ReadAsync<QueueCountRow>()).ToList();

        return new FailedMessagesSummaryDto(
            now,
            pending?.PendingCount ?? 0,
            pending?.OldestPendingAt,
            last24h ?? new Last24hDto(0, 0, 0),
            topGroups,
            outboxCounts?.FailedCount ?? 0,
            outboxCounts?.StuckCount ?? 0,
            BuildNodes(snapshots, queueCounts));
    }

    private static List<NodeSnapshotDto> BuildNodes(
        List<BrokerSnapshotRow> snapshots, List<QueueCountRow> queueCounts)
    {
        var countsByNode = queueCounts.ToLookup(c => c.Node);
        var snapshotNodes = snapshots.Select(s => s.Node).ToHashSet(StringComparer.Ordinal);

        var fromSnapshots = snapshots.Select(snapshot =>
        {
            var storedByName = DeserializeQueues(snapshot.QueuesJson).ToDictionary(q => q.Name);
            var countsForNode = countsByNode[snapshot.Node].ToList();
            var countsByQueue = countsForNode.ToLookup(c => c.SourceQueue);

            var queueNames = storedByName.Keys
                .Union(countsForNode.Select(c => c.SourceQueue))
                .Distinct();

            var queues = queueNames.Select(name =>
            {
                storedByName.TryGetValue(name, out var stored);
                var errorCount = countsByQueue[name]
                    .FirstOrDefault(c => c.Kind == FailedMessageKind.Error)?.Count ?? 0;
                var skippedCount = countsByQueue[name]
                    .FirstOrDefault(c => c.Kind == FailedMessageKind.Skipped)?.Count ?? 0;

                return new QueueHealthDto(
                    name,
                    stored?.Ready is { } ready ? (int)ready : null,
                    stored?.Unacked is { } unacked ? (int)unacked : null,
                    stored?.Consumers,
                    stored?.PublishRate,
                    stored?.DeliverRate,
                    stored?.Samples ?? [],
                    errorCount,
                    skippedCount,
                    OrderedEndpoints.IsOrdered(name));
            }).ToList();

            return new NodeSnapshotDto(
                snapshot.Node, snapshot.CollectedAt, snapshot.ManagementStatus, snapshot.LastError, queues);
        });

        // A node can have Pending FailedMessages rows before its collector has ever
        // completed a Snapshot round (or after its BrokerSnapshot row was somehow lost) — it still needs
        // to appear, just with no managementStatus/collectedAt/lastError and queue health built purely
        // from the FailedMessages counts (ready/unacked/etc. omitted, samples empty).
        var ghostNodes = countsByNode
            .Where(g => !snapshotNodes.Contains(g.Key))
            .Select(g =>
            {
                var queues = g
                    .GroupBy(c => c.SourceQueue)
                    .Select(byQueue =>
                    {
                        var errorCount = byQueue.FirstOrDefault(c => c.Kind == FailedMessageKind.Error)?.Count ?? 0;
                        var skippedCount = byQueue.FirstOrDefault(c => c.Kind == FailedMessageKind.Skipped)?.Count ?? 0;
                        return new QueueHealthDto(
                            byQueue.Key, null, null, null, null, null, [], errorCount, skippedCount,
                            OrderedEndpoints.IsOrdered(byQueue.Key));
                    })
                    .ToList();

                return new NodeSnapshotDto(g.Key, null, null, null, queues);
            });

        return fromSnapshots.Concat(ghostNodes).ToList();
    }

    private static List<StoredQueueSnapshot> DeserializeQueues(string json) =>
        JsonSerializer.Deserialize<List<StoredQueueSnapshot>>(json, CamelCaseOptions) ?? [];

    private record PendingRow(int PendingCount, DateTime? OldestPendingAt);

    private record BrokerSnapshotRow(
        string Node, DateTime CollectedAt, string ManagementStatus, string QueuesJson, string? LastError);

    private record QueueCountRow(string Node, string SourceQueue, string Kind, int Count);

    // SUM(CASE…) over zero UNION rows (all six outbox tables empty) returns SQL NULL, not 0 — nullable so
    // Dapper doesn't reject the binding; coalesced to 0 where the DTO is built.
    private record OutboxCountsRow(int? FailedCount, int? StuckCount);

    private record StoredQueueSnapshot(
        string Name, long? Ready, long? Unacked, int? Consumers, double? PublishRate, double? DeliverRate,
        List<int> Samples);
}
