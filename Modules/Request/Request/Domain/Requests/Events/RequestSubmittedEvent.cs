namespace Request.Domain.Requests.Events;

public record RequestSubmittedEvent(Request Request, string? GroupTag, string? EntrySource = null) : IDomainEvent;