namespace Appraisal.Domain.Appraisals.Events;

public record DocumentUnlinkedEvent(Guid PricingId, Guid DocumentId) : IDomainEvent;
