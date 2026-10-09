using Shared.CQRS;

namespace Integration.Application.Features.FailedMessages.GetFailedMessage;

public record GetFailedMessageQuery(Guid Id) : IQuery<FailedMessageDetailDto>;

public record FailedMessageDetailDto(
    Guid Id,
    string Node,
    string SourceQueue,
    string Kind,
    string Status,
    Guid? MessageId,
    Guid? ConversationId,
    string MessageType,
    string? ConsumerType,
    string ExceptionType,
    string ExceptionMessage,
    string? StackTrace,
    int RetryCount,
    DateTime FaultedAt,
    DateTime CollectedAt,
    string? RefType,
    Guid? RefId,
    string? RefNumber,
    string? ContentType,
    Dictionary<string, string> Headers,
    string Body,
    string? ActionBy,
    DateTime? ActionAt,
    string? ActionReason,
    bool IsOrderedQueue,
    bool IsNonTransient,
    IReadOnlyList<FailedMessageSiblingDto> Siblings,
    IReadOnlyList<FailedMessageHistoryEntryDto> History,
    DateTime? RetryAvailableAt
);

public record FailedMessageSiblingDto(Guid Id, string SourceQueue, string Kind, string Status, DateTime FaultedAt);

/// <summary>ActorCode is null (omitted on the wire) for a server-generated <c>RetryFailed</c> entry —
/// design D6; the acting node name is embedded in <see cref="Reason"/> instead.</summary>
public record FailedMessageHistoryEntryDto(string Action, string? ActorCode, string? Reason, DateTime At);
