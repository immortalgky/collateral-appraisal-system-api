namespace Request.RequestTitles.Features.RemoveRequestTitle;

public record RemoveRequestTitleCommand(Guid RequestId, Guid Id) : ICommand<RemoveRequestTitleResult>;