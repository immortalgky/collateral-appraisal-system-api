using System.Text.Json.Serialization;
using Shared.CQRS;
using Shared.Pagination;

namespace Integration.Application.Features.OutboxMessages.GetOutboxMessages;

public record GetOutboxMessagesQuery(
    int PageNumber,
    int PageSize,
    string Status,
    string? Module,
    string? Search
) : IQuery<PaginatedResult<OutboxMessageListDto>>;

public record OutboxMessageListDto(
    string Module,
    Guid Id,
    string EventType,
    string? CorrelationId,
    DateTime OccurredAt,
    DateTime? ProcessedAt,
    DateTime? ProcessingStartedAt,
    string? Error,
    int RetryCount,
    string Status,
    string? RefType,
    Guid? RefId,
    string? RefNumber,
    // null = unknown (Processed history older than the outbox retention is purged); emitted as an
    // explicit null, never omitted, so a client can tell "can't verify" from a missing field.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? NewerSentCount,
    bool TypeResolvable,
    // "Disallowed" | "Unresolvable" | "Deserialization" | null (any other error, or not Failed); emitted as an
    // explicit null like NewerSentCount, never omitted.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? FailureClass
);
