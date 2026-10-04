using System.Text;
using System.Text.Json;
using Integration.FailedMessages;
using Shared.CQRS;
using Shared.Data;
using Shared.Exceptions;
using Shared.Pagination;
using Shared.Time;

namespace Integration.Application.Features.FailedMessages.GetFailedMessage;

public class GetFailedMessageQueryHandler(
    ISqlConnectionFactory sqlConnectionFactory,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetFailedMessageQuery, FailedMessageDetailDto>
{
    public async Task<FailedMessageDetailDto> Handle(GetFailedMessageQuery request, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Id, Node, SourceQueue, Kind, Status, MessageId, ConversationId, MessageType, ConsumerType,
                   ExceptionType, ExceptionMessage, StackTrace, RetryCount, FaultedAt, CollectedAt,
                   RefType, RefId, RefNumber, ContentType, Headers, Body, ActionBy, ActionAt, ActionReason
            FROM integration.FailedMessages
            WHERE Id = @Id
            """;

        var row = await sqlConnectionFactory.QueryFirstOrDefaultAsync<FailedMessageDetailRow>(sql, new { request.Id })
                  ?? throw new NotFoundException("FailedMessage", request.Id);

        // Design D11/D13: siblings share MessageId (dedup key), excluding this row itself.
        var siblings = row.MessageId is null
            ? []
            : (await sqlConnectionFactory.QueryAsync<FailedMessageSiblingDto>(
                """
                SELECT Id, SourceQueue, Kind, Status, FaultedAt
                FROM integration.FailedMessages
                WHERE MessageId = @MessageId AND Id <> @Id
                """,
                new { row.MessageId, request.Id })).ToList();

        var history = (await sqlConnectionFactory.QueryAsync<FailedMessageHistoryEntryDto>(
            """
            SELECT Action, ActorCode, Reason, At
            FROM integration.FailedMessageAuditLogs
            WHERE TargetId = @Id
            ORDER BY At
            """,
            new { request.Id })).ToList();

        var headers = string.IsNullOrWhiteSpace(row.Headers)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(row.Headers) ?? new Dictionary<string, string>();
        // Raw body: anyone holding FAILED_MESSAGE_VIEW is entitled to see the data.
        var body = Encoding.UTF8.GetString(row.Body);

        var now = dateTimeProvider.ApplicationNow;

        return new FailedMessageDetailDto(
            row.Id, row.Node, row.SourceQueue, row.Kind, row.Status, row.MessageId, row.ConversationId,
            row.MessageType, row.ConsumerType, row.ExceptionType, row.ExceptionMessage,
            row.StackTrace, row.RetryCount, row.FaultedAt, row.CollectedAt, row.RefType, row.RefId, row.RefNumber,
            row.ContentType, headers, body,
            row.ActionBy, row.ActionAt, row.ActionReason,
            OrderedEndpoints.IsOrdered(row.SourceQueue), ExceptionTypeClassifier.IsNonTransient(row.ExceptionType),
            siblings, history,
            FailedMessageRetryPolicy.RetryAvailableAtOrNull(row.Status, row.FaultedAt, row.CollectedAt, now));
    }

    private record FailedMessageDetailRow(
        Guid Id, string Node, string SourceQueue, string Kind, string Status, Guid? MessageId, Guid? ConversationId,
        string MessageType, string? ConsumerType, string ExceptionType, string ExceptionMessage,
        string? StackTrace, int RetryCount, DateTime FaultedAt, DateTime CollectedAt, string? RefType, Guid? RefId, string? RefNumber,
        string? ContentType, string Headers, byte[] Body, string? ActionBy, DateTime? ActionAt, string? ActionReason);
}
