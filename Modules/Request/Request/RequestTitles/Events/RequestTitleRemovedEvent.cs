namespace Request.RequestTitles.Events;

public record RequestTitleRemovedEvent(long RequestId, long TitleId, string CollateralType) : IDomainEvent;