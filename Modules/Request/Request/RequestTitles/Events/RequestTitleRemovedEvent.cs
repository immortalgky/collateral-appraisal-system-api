namespace Request.RequestTitles.Events;

public record RequestTitleRemovedEvent(Guid RequestId, long TitleId, string CollateralType) : IDomainEvent;