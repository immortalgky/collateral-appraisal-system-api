namespace Request.RequestTitles.Events;

public record RequestTitleRemovedEvent(Guid RequestId, Guid TitleId, string CollateralType) : IDomainEvent;