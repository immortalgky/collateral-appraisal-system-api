using System.Data;
using Dapper;
using Integration.FailedMessages;
using Shared.CQRS;
using Shared.Data;
using Shared.Messaging.Services;
using Shared.Pagination;
using Shared.Time;

namespace Integration.Application.Features.OutboxMessages.GetOutboxMessages;

public class GetOutboxMessagesQueryHandler(
    ISqlConnectionFactory sqlConnectionFactory,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetOutboxMessagesQuery, PaginatedResult<OutboxMessageListDto>>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "Every [{module}] schema name interpolated here (the id-only page UNION built by OutboxUnionSql, " +
            "and the per-module display-column/newer-sent queries for just that page's rows below it) comes " +
            "only from OutboxModuleWhitelist.Modules — module/group.Key is the UNION's own synthetic Module " +
            "literal, never request.Module or any " +
            "other caller input directly. request.Module only SELECTS which whitelist branch Build emits " +
            "(it is validated by GetOutboxMessagesQueryValidator and again inside OutboxUnionSql.Build, and " +
            "only the whitelist's own literal is interpolated). Every actual value " +
            "(Ids, Search, StuckThreshold) is bound as a @parameter, never interpolated.")]
    public async Task<PaginatedResult<OutboxMessageListDto>> Handle(
        GetOutboxMessagesQuery request, CancellationToken cancellationToken)
    {
        var conditions = new List<string>();
        var resentOnly = false;
        var parameters = new DynamicParameters();

        switch (request.Status)
        {
            case "Stuck":
                // A DISPLAY-only threshold (2 min), independent of either reset rule (see
                // OutboxUnionSql.StuckPredicate, which the summary's stuck count shares).
                conditions.Add(OutboxUnionSql.StuckPredicate);
                parameters.AddStuckThreshold(dateTimeProvider.ApplicationNow);
                break;
            case "Resent":
                resentOnly = true; // per-branch condition (needs the branch's module), added in WhereFor below
                break;
            case "All":
                break;
            default: // "Failed" (default per contract)
                conditions.Add("o.Status = 'Failed'");
                break;
        }

        // Selects the one UNION branch instead of filtering after it. Build re-checks it against the whitelist
        // and only ever interpolates the whitelist's own literal, never request.Module itself.
        var onlyModule = string.IsNullOrWhiteSpace(request.Module) ? null : request.Module;

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Same LIKE-escaping as the failed-messages search. LikePattern.Escape is byte-for-byte the same escaping SqlLikeEscaper duplicated
            // (%, _, [, and \ itself, in the same order, same ESCAPE '\' convention) — reuse it instead.
            conditions.Add("(o.EventType LIKE @Search ESCAPE '\\' OR o.CorrelationId LIKE @Search ESCAPE '\\')");
            parameters.Add("Search", $"%{LikePattern.Escape(request.Search)}%");
        }

        // Every predicate goes INSIDE each per-module branch (OutboxUnionSql's whereClause), so a page no
        // longer scans every branch — Processed rows included — only to discard rows after the UNION.
        string? WhereFor(string module)
        {
            // A branch has no Module column (it is a UNION literal), so the Resent check names the module itself.
            var all = resentOnly
                ? conditions.Append(
                    $"""
                    EXISTS (SELECT 1 FROM integration.FailedMessageAuditLogs a
                            WHERE a.Action = 'OutboxResend' AND a.OutboxModule = N'{module}' AND a.TargetId = o.Id)
                    """).ToList()
                : conditions;
            return all.Count == 0 ? null : string.Join(" AND ", all);
        }

        // Page over (Module, Id, OccurredAt, Status) ONLY — the Failed and All tabs (no Search, not Resent) can
        // answer that from the primary key or IX_IntegrationEventOutbox_Polling (Status, OccurredAt) without
        // reading the rows. Search (EventType/CorrelationId LIKE), Stuck (ProcessingStartedAt) and Resent
        // (the audit-log EXISTS) still read the rows to evaluate their predicate, but never carry the in-row
        // payload through the UNION's sort, which paging over EventType/CorrelationId used to (a clustered scan
        // of all six tables and a full sort before OFFSET). The display columns are fetched below for just the
        // page's rows.
        var union = OutboxUnionSql.Build("Id, OccurredAt, Status", WhereFor, onlyModule);

        var sql = $"""
            SELECT o.Module, o.Id, o.OccurredAt, o.Status
            FROM ({union}) o
            """;

        var paginationRequest = new PaginationRequest(request.PageNumber - 1, request.PageSize);
        // OccurredAt is not unique, and an Id is only unique within one module's table —
        // Module + Id is the full tie-breaker, so OFFSET/FETCH can't repeat or skip rows across pages.
        var raw = await sqlConnectionFactory.QueryPaginatedAsync<OutboxPageRow>(
            sql, "o.OccurredAt DESC, o.Module, o.Id DESC", paginationRequest, parameters);

        var now = dateTimeProvider.ApplicationNow;
        var pageRows = raw.Items.ToList();
        if (pageRows.Count == 0)
            return new PaginatedResult<OutboxMessageListDto>([], raw.Count, request.PageNumber, request.PageSize);

        // Exactly one display-column/Payload fetch and one newerSentCount fetch PER MODULE PRESENT
        // ON THE PAGE (never per row) — `module` here is always one of OutboxUnionSql's own synthetic
        // `Module` literals (from OutboxModuleWhitelist.Modules), never caller input.
        var details = new Dictionary<(string Module, Guid Id), OutboxDetailRow>();
        var newerSentCounts = new Dictionary<(string Module, Guid Id), int>();

        foreach (var group in pageRows.GroupBy(r => r.Module))
        {
            var module = group.Key;
            var rows = group.ToList();

            var detailRows = await sqlConnectionFactory.QueryAsync<OutboxDetailRow>(
                $"""
                SELECT Id, EventType, CorrelationId, ProcessingStartedAt, Payload, Error, ProcessedAt, RetryCount
                FROM [{module}].[IntegrationEventOutbox]
                WHERE Id IN @Ids
                """,
                new { Ids = rows.Select(r => r.Id).ToArray() });
            foreach (var detail in detailRows)
                details[(module, detail.Id)] = detail;

            // A row older than the Processed retention window is not queried at all: its count is unknown
            // (NewerSentCountRule), so it is reported as null below instead of a misleading 0.
            var withCorrelation = rows
                .Where(r => !NewerSentCountRule.HistoryMayBePurged(r.OccurredAt, now)
                            && details.GetValueOrDefault((module, r.Id))?.CorrelationId is not null)
                .Select(r => (r.Id, CorrelationId: details[(module, r.Id)].CorrelationId!, r.OccurredAt))
                .ToList();
            if (withCorrelation.Count == 0)
                continue;

            var (valuesSql, valuesParams) = BuildNewerSentPageValues(withCorrelation);
            var counts = await sqlConnectionFactory.QueryAsync<NewerSentCountRow>(
                $"""
                SELECT p.Id, COUNT(o.Id) AS NewerSentCount
                FROM (VALUES {valuesSql}) AS p(Id, CorrelationId, OccurredAt)
                LEFT JOIN [{module}].[IntegrationEventOutbox] o
                    ON o.CorrelationId = p.CorrelationId AND o.Status = 'Processed' AND o.OccurredAt > p.OccurredAt
                GROUP BY p.Id
                """,
                valuesParams);
            foreach (var count in counts)
                newerSentCounts[(module, count.Id)] = count.NewerSentCount;
        }

        var items = BuildItems(pageRows, details, newerSentCounts, now);

        return new PaginatedResult<OutboxMessageListDto>(items, raw.Count, request.PageNumber, request.PageSize);
    }

    /// <summary>
    /// Merges the page query's rows with the per-module display-column fetch. Every page row yields exactly one
    /// item, in page order — the page's count comes from the id query, so dropping a row (purged, or changed,
    /// between the two queries) would leave a short or empty page while the total is still positive. A row with
    /// no detail keeps what the page query read and has empty/null detail fields. Status comes from the page
    /// query for every row, so a row listed under one tab never shows another status because of the later read.
    /// </summary>
    internal static List<OutboxMessageListDto> BuildItems(
        IReadOnlyList<OutboxPageRow> pageRows,
        IReadOnlyDictionary<(string Module, Guid Id), OutboxDetailRow> details,
        IReadOnlyDictionary<(string Module, Guid Id), int> newerSentCounts,
        DateTime now)
    {
        var items = new List<OutboxMessageListDto>(pageRows.Count);
        foreach (var row in pageRows)
        {
            if (!details.TryGetValue((row.Module, row.Id), out var detail))
            {
                // NewerSentCount null = unknown: without the CorrelationId "none newer" can't be claimed.
                items.Add(new OutboxMessageListDto(
                    row.Module, row.Id, "", null, row.OccurredAt, null, null, null, 0, row.Status,
                    null, null, null, null, false, null));
                continue;
            }

            var (refType, refId, refNumber) = FailedMessageReferenceResolver.Resolve(detail.Payload);

            var resolution = IntegrationEventNamespace.TryResolve(detail.EventType, out _);

            items.Add(new OutboxMessageListDto(
                row.Module, row.Id, detail.EventType, detail.CorrelationId, row.OccurredAt, detail.ProcessedAt,
                detail.ProcessingStartedAt, detail.Error, detail.RetryCount, row.Status,
                refType, refId, refNumber, NewerSentCountFor(row, detail.CorrelationId),
                resolution == TypeResolution.Resolved,
                OutboxFailureClass.Classify(row.Status, detail.Error, resolution)));
        }

        return items;

        // No CorrelationId = no sibling group, so 0 is certain; an old row's history may be purged = unknown.
        int? NewerSentCountFor(OutboxPageRow row, string? correlationId) =>
            correlationId is null ? 0
            : NewerSentCountRule.HistoryMayBePurged(row.OccurredAt, now) ? null
            : newerSentCounts.GetValueOrDefault((row.Module, row.Id));
    }

    /// <summary>Builds a `(VALUES ...)` table constructor for one module's page rows — everything is a
    /// parameter, nothing interpolated, so this stays safe with an arbitrary CorrelationId value.</summary>
    private static (string Sql, DynamicParameters Parameters) BuildNewerSentPageValues(
        IReadOnlyList<(Guid Id, string CorrelationId, DateTime OccurredAt)> rows)
    {
        var parameters = new DynamicParameters();
        var clauses = new List<string>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            clauses.Add($"(@Id{i}, @Cid{i}, @At{i})");
            parameters.Add($"Id{i}", rows[i].Id);
            parameters.Add($"Cid{i}", rows[i].CorrelationId);
            // DateTime2 to match the datetime2 OccurredAt it is compared with (a plain DateTime would round).
            parameters.Add($"At{i}", rows[i].OccurredAt, DbType.DateTime2);
        }

        return (string.Join(", ", clauses), parameters);
    }

    internal record OutboxPageRow(string Module, Guid Id, DateTime OccurredAt, string Status);

    // Positional: the SELECT above lists these columns in exactly this order.
    internal record OutboxDetailRow(
        Guid Id, string EventType, string? CorrelationId, DateTime? ProcessingStartedAt,
        string Payload, string? Error, DateTime? ProcessedAt, int RetryCount);

    private record NewerSentCountRow(Guid Id, int NewerSentCount);
}
