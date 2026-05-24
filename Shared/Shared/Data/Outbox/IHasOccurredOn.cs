namespace Shared.Data.Outbox;

/// <summary>
/// Marker for events that carry an `OccurredOn` timestamp. The integration-event outbox
/// stamps `ApplicationNow` on the publish boundary when the value is the default.
/// Lives in <c>Shared</c> so the outbox (which can't reference <c>Shared.Messaging</c>) can
/// stamp without a project-reference cycle.
/// </summary>
public interface IHasOccurredOn
{
    DateTime OccurredOn { get; set; }
}
