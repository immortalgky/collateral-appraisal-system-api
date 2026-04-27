namespace Appraisal.Domain.Projects.Events;

/// <summary>
/// Domain event raised when a new project is created.
/// </summary>
public record ProjectCreatedEvent(Project Project) : IDomainEvent;
