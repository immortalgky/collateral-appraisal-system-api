namespace Request.RequestTitles.Features.RemoveRequestTitle;

public record RemoveRequestTitleCommand(long RequestId, long Id) : ICommand<RemoveRequestTitleResult>;