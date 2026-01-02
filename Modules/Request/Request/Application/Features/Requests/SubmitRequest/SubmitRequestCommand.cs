namespace Request.Application.Features.Requests.SubmitRequest;

public record SubmitRequestCommand(Guid Id) : ICommand<SubmitRequestResult>, ITransactionalCommand<IRequestUnitOfWork>;