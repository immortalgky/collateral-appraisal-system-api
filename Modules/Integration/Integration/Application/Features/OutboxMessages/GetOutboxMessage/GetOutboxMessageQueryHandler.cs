using System.Data;
using System.Text.Json;
using Integration.Application.Features.OutboxMessages;
using Integration.FailedMessages;
using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Exceptions;
using Shared.Messaging.Services;
using Shared.Pagination;
using Shared.Time;

namespace Integration.Application.Features.OutboxMessages.GetOutboxMessage;

public class GetOutboxMessageQueryHandler(
    ISqlConnectionFactory sqlConnectionFactory,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetOutboxMessageQuery, OutboxMessageDetailDto>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "request.Module is checked against OutboxModuleWhitelist.EnsureValid immediately above, before " +
            "either interpolated [{request.Module}] query below is ever built (defence in depth — the " +
            "validator already restricts it too). Id/CorrelationId/OccurredAt are bound as @parameters.")]
    public async Task<OutboxMessageDetailDto> Handle(GetOutboxMessageQuery request, CancellationToken cancellationToken)
    {
        // Re-checked here (validator already restricts Module before MediatR reaches this handler)
        // before it is interpolated into the schema-qualified query below.
        OutboxModuleWhitelist.EnsureValid(request.Module);

        var row = await sqlConnectionFactory.QueryFirstOrDefaultAsync<OutboxMessageDetailRow>(
            $"""
            SELECT Id, EventType, Payload, Headers, CorrelationId, OccurredAt, ProcessedAt,
                   ProcessingStartedAt, Error, RetryCount, Status
            FROM [{request.Module}].[IntegrationEventOutbox]
            WHERE Id = @Id
            """,
            new { request.Id })
            ?? throw new NotFoundException("OutboxMessage", request.Id);

        var (refType, refId, refNumber) = FailedMessageReferenceResolver.Resolve(row.Payload);

        // null = unknown: a row older than the Processed retention window may have had its newer siblings
        // purged (NewerSentCountRule). No CorrelationId = no sibling group, so 0 is certain.
        int? newerSentCount;
        if (row.CorrelationId is null)
            newerSentCount = 0;
        else if (NewerSentCountRule.HistoryMayBePurged(row.OccurredAt, dateTimeProvider.ApplicationNow))
            newerSentCount = null;
        else
        {
            // OccurredAt as DateTime2: a plain DateTime binds as SqlDbType.DateTime (3.33 ms rounding)
            // and is compared with the datetime2 column.
            var countParams = new DynamicParameters();
            countParams.Add("CorrelationId", row.CorrelationId);
            countParams.Add("OccurredAt", row.OccurredAt, DbType.DateTime2);
            newerSentCount = await sqlConnectionFactory.ExecuteScalarAsync<int>(
                $"""
                SELECT COUNT(*) FROM [{request.Module}].[IntegrationEventOutbox]
                WHERE CorrelationId = @CorrelationId AND Status = 'Processed' AND OccurredAt > @OccurredAt
                """,
                countParams);
        }

        var headers = string.IsNullOrWhiteSpace(row.Headers)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(row.Headers) ?? new Dictionary<string, string>();

        var resolution = IntegrationEventNamespace.TryResolve(row.EventType, out _);

        return new OutboxMessageDetailDto(
            request.Module, row.Id, row.EventType, row.CorrelationId, row.OccurredAt, row.ProcessedAt,
            row.ProcessingStartedAt, row.Error, row.RetryCount, row.Status,
            refType, refId, refNumber, newerSentCount,
            resolution == TypeResolution.Resolved,
            OutboxFailureClass.Classify(row.Status, row.Error, resolution),
            row.Payload, headers);
    }

    private record OutboxMessageDetailRow(
        Guid Id, string EventType, string Payload, string? Headers, string? CorrelationId, DateTime OccurredAt,
        DateTime? ProcessedAt, DateTime? ProcessingStartedAt, string? Error, int RetryCount, string Status);
}
