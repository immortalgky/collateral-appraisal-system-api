namespace Request.Requests.Features.AddCommentToRequest;

public record AddCommentToRequestCommand(long Id, string Comment) : ICommand<AddCommentToRequestResult>;