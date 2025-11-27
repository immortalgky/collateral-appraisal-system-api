using MediatR;
using Microsoft.Extensions.Logging;
using Shared.CQRS;
using Shared.Data;

namespace Shared.Behaviors;

public class TransactionalBehavior<TRequest, TResponse>(
    IServiceProvider serviceProvider,
    ILogger<TransactionalBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
    where TResponse : notnull

{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        TResponse? response;

        // Get the UOW type from the command's marker interface
        var transactionalInterface = typeof(TRequest)
            .GetInterfaces()
            .FirstOrDefault(i =>
                i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(ITransactionalCommand<>));

        if (transactionalInterface == null)
            // Not a transactional command, proceed without transaction
            return await next(cancellationToken);

        var unitOfWorkType = transactionalInterface.GetGenericArguments()[0];

        if (serviceProvider.GetService(unitOfWorkType) is not IUnitOfWork unitOfWork)
        {
            logger.LogWarning("[TRANSACTION] No UnitOfWork found for transactional command {Command}",
                typeof(TRequest).Name);
            return await next(cancellationToken);
        }

        logger.LogInformation("[TRANSACTION] Beginning transaction for command {Command}", typeof(TRequest).Name);

        if (unitOfWork.HasActiveTransaction)
        {
            logger.LogWarning(
                "[TRANSACTION] An active transaction already exists for command {Command}, proceeding without starting a new one",
                typeof(TRequest).Name);
            return await next(cancellationToken);
        }

        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);

            response = await next(cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            logger.LogInformation("[TRANSACTION] Committed transaction for command {Command}",
                typeof(TRequest).Name);
        }
        catch (Exception e)
        {
            logger.LogError(
                "[TRANSACTION] Rolling back transaction for command {Command} due to error: {Error}",
                typeof(TRequest).Name, e.Message);
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return response!;
    }
}