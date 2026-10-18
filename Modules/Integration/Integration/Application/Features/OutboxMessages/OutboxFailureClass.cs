using Shared.Messaging.Services;

namespace Integration.Application.Features.OutboxMessages;

/// <summary>
/// Server-side <c>failureClass</c> of an outbox row, so the client never matches error text itself.
/// Classified from the prefixes <c>IntegrationEventDeliveryService</c> writes (<see cref="OutboxFailureReasons"/>)
/// and what the row's <c>EventType</c> resolves to NOW, and only for a <c>Failed</c> row — any other status, or any
/// other error, is <c>null</c>.
/// </summary>
public static class OutboxFailureClass
{
    public const string Disallowed = "Disallowed";
    public const string Unresolvable = "Unresolvable";
    public const string Deserialization = "Deserialization";

    /// <param name="resolution">What <c>IntegrationEventNamespace.TryResolve</c> returns for the row's EventType
    /// at read time. Needed because rows failed by the OLD delivery code carry "Disallowed type:" for an
    /// unresolvable type too (it merged the two cases): such a legacy row is <c>Disallowed</c> — the permanent
    /// don't-resend class — only when its type really resolves to a disallowed one. If it is unresolvable, or now
    /// resolves to an allowed type (deployed since), it is <c>Unresolvable</c>, and the client compares
    /// <c>typeResolvable</c> to show "type now resolves".</param>
    public static string? Classify(string status, string? error, TypeResolution resolution)
    {
        if (status != "Failed" || error is null)
            return null;

        if (error.StartsWith(OutboxFailureReasons.Disallowed, StringComparison.Ordinal))
            return resolution == TypeResolution.Disallowed ? Disallowed : Unresolvable;
        if (error.StartsWith(OutboxFailureReasons.Unresolvable, StringComparison.Ordinal))
            return Unresolvable;
        if (error.StartsWith(OutboxFailureReasons.DeserializationFailed, StringComparison.Ordinal)
            || error.StartsWith(OutboxFailureReasons.DeserializationReturnedNull, StringComparison.Ordinal))
            return Deserialization;

        return null;
    }
}
