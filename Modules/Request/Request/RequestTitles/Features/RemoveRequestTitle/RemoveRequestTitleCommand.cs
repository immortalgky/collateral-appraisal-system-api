namespace Request.RequestTitles.Features.RemoveRequestTitle;

public record RemoveRequestTitleCommand(Guid RequestId, long Id) : ICommand<RemoveRequestTitleResult>;