namespace Request.Requests.Features.DeleteRequest;

public record DeleteRequestCommand(Guid Id) : ICommand<DeleteRequestResult>;