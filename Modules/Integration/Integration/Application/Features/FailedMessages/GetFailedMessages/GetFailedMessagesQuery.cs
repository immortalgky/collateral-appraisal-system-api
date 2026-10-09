using Shared.CQRS;
using Shared.Pagination;

namespace Integration.Application.Features.FailedMessages.GetFailedMessages;

public record GetFailedMessagesQuery(
    int PageNumber,
    int PageSize,
    string Status,
    string? Queue,
    string? Node,
    string? ExceptionType,
    string? Search
) : IQuery<PaginatedResult<FailedMessageListDto>>;

public record FailedMessageListDto(
    Guid Id,
    string Node,
    string SourceQueue,
    string Kind,
    string Status,
    Guid? MessageId,
    string MessageType,
    string? ConsumerType,
    string ExceptionType,
    string ExceptionMessage,
    int RetryCount,
    DateTime FaultedAt,
    DateTime CollectedAt,
    string? RefType,
    Guid? RefId,
    string? RefNumber,
    string? ActionBy,
    DateTime? ActionAt,
    DateTime? RetryAvailableAt,
    bool IsOrderedQueue,
    bool IsNonTransient,
    int SiblingCount
);
