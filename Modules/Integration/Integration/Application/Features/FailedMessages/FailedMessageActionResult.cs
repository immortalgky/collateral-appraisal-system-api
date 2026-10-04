namespace Integration.Application.Features.FailedMessages;

/// <summary>
/// Shared result shape for the bulk retry and discard commands (see api-contract.md).
/// </summary>
public record FailedMessageActionResult(
    IReadOnlyList<Guid> Accepted,
    IReadOnlyList<SkippedFailedMessage> Skipped);

public record SkippedFailedMessage(Guid Id, string Reason, string? By, DateTime? At);
