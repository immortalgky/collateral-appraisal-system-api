namespace Request.Domain.Requests.Events;

public record RequestCancelledEvent(Guid RequestId, string CancelledBy, string? Reason) : IDomainEvent;
