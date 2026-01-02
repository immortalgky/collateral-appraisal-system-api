namespace Request.Domain.RequestTitles.Events;

public record TitleDocumentDetachedEvent(
    Guid TitleDocumentId,
    Guid TitleId,
    Guid? DocumentId) : IDomainEvent;
