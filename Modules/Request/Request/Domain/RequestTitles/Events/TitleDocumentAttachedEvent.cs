namespace Request.Domain.RequestTitles.Events;

public record TitleDocumentAttachedEvent(TitleDocument TitleDocument) : IDomainEvent;
