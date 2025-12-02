namespace Request.Requests.Features.SubmitRequest;

public record SubmitRequestCommand(Guid Id) : ICommand<SubmitRequestResult>;