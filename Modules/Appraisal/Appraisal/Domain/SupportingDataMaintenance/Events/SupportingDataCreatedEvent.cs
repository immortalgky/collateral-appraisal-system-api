namespace Appraisal.Domain.SupportingDataMaintenance.Events;

public record SupportingDataCreatedEvent(Guid SupportingDataId) : IDomainEvent;