using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Shared.Behaviors;

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
    where TResponse : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        logger.LogInformation("[START] Handle request={Request} - Response={Response} - {Request}", typeof(TRequest).Name, typeof(TResponse).Name, request);

        var timer = Stopwatch.StartNew();

        var response = await next(cancellationToken);

        timer.Stop();
        var timeTaken = timer.Elapsed;
        if (timeTaken.TotalSeconds > 3)
        {
            logger.LogWarning("[PERFORMANCE] The request {Request} took {TimeTaken:0.00} seconds", typeof(TRequest).Name, timeTaken.TotalSeconds);
        }

        logger.LogInformation("[END] Handled {Request} with {Response}", typeof(TRequest).Name, typeof(TResponse).Name);
        return response;
    }
}
