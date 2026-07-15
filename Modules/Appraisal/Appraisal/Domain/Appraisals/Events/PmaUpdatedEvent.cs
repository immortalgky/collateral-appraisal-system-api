namespace Appraisal.Domain.Appraisals.Events;

/// <summary>
/// Domain event raised when a PMA (Land &amp; Building or Condo) property is saved, so the outbox
/// row commits atomically with the PMA data and the update can be propagated asynchronously to the
/// external LOS system.
/// </summary>
public record PmaUpdatedEvent(Guid AppraisalId, Guid PropertyId) : IDomainEvent;
