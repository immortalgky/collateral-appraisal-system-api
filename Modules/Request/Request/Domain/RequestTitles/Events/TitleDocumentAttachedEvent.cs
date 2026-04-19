namespace Request.Domain.RequestTitles.Events;

public record TitleDocumentAttachedEvent(Guid RequestId, TitleDocument TitleDocument) : IDomainEvent;
