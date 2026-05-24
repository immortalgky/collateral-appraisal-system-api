using MediatR;

namespace Shared.DDD;

public interface IDomainEvent : INotification
{
    Guid EventId => Guid.CreateVersion7();

    /// <summary>
    /// Default is <c>DateTime.MinValue</c>. Concrete domain events that need a real
    /// timestamp must override this property and set it from the caller's clock —
    /// the MediatR dispatcher does NOT auto-stamp <c>IDomainEvent</c> like
    /// <see cref="Shared.Data.Outbox.IntegrationEventOutbox"/> stamps
    /// <see cref="Shared.Data.Outbox.IHasOccurredOn"/> at the publish boundary.
    /// </summary>
    public DateTime OccurredOn => default;
    public string EventType => GetType().AssemblyQualifiedName!;
}
