using Shared.CQRS;

namespace Integration.Application.Features.FailedMessages.RetryFailedMessages;

public record RetryFailedMessagesCommand(IReadOnlyList<Guid> Ids, string? Reason, string? IpAddress)
    : ICommand<FailedMessageActionResult>;
