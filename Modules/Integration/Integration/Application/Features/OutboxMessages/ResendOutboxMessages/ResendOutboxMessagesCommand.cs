using Shared.CQRS;

namespace Integration.Application.Features.OutboxMessages.ResendOutboxMessages;

public record ResendOutboxMessagesCommand(IReadOnlyList<OutboxMessageRef> Items, string? Reason, string? IpAddress)
    : ICommand<OutboxResendResult>;

public record OutboxMessageRef(string Module, Guid Id);

public record OutboxResendResult(
    IReadOnlyList<OutboxMessageRef> Accepted,
    IReadOnlyList<OutboxResendSkipped> Skipped);

public record OutboxResendSkipped(string Module, Guid Id, string Reason);
