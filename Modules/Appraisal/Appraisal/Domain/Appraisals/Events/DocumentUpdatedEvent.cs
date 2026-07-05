namespace Appraisal.Domain.Appraisals.Events;

public record DocumentUpdatedEvent(Guid PricingId, Guid PreviousDocumentId, Guid DocumentId) : IDomainEvent;
