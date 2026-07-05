namespace Appraisal.Domain.Appraisals.Events;

public record DocumentLinkedEvent(Guid PricingId, Guid DocumentId) : IDomainEvent;
