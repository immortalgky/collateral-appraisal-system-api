namespace Assignment.Assignments.Events;

public record AssignmentCreatedEvent(Models.Assignment Assignment) : IDomainEvent;