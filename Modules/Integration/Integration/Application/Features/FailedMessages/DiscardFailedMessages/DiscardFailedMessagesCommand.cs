using Shared.CQRS;

namespace Integration.Application.Features.FailedMessages.DiscardFailedMessages;

public record DiscardFailedMessagesCommand(IReadOnlyList<Guid> Ids, string Reason, string? IpAddress)
    : ICommand<FailedMessageActionResult>;
