namespace Request.Application.Features.Requests.DeleteRequest;

public record DeleteRequestCommand(Guid Id) : ICommand<DeleteRequestResult>, ITransactionalCommand<IRequestUnitOfWork>;