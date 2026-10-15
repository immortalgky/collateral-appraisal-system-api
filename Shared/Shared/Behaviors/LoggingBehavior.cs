using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.CQRS;

namespace Shared.Behaviors;

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
    where TResponse : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var isQuery = request is IQuery<TResponse>;
        var timer = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken);
            timer.Stop();
            Log(request, "ok", timer.Elapsed, isQuery);
            return response;
        }
        catch
        {
            timer.Stop();
            Log(request, "failed", timer.Elapsed, isQuery);
            throw;
        }
    }

    private void Log(TRequest request, string outcome, TimeSpan elapsed, bool isQuery)
    {
        // Logs the whole request via its ToString. A command carrying a password or secret MUST
        // override ToString to redact it (see ChangePasswordCommand, CreateWebhookSubscriptionCommand),
        // or the value lands in Seq and the application log table in plaintext.
        const string template = "[HANDLED] {RequestName} {Outcome} in {ElapsedMs} ms {Request}";
        var name = typeof(TRequest).Name;
        var ms = Math.Round(elapsed.TotalMilliseconds);

        if (outcome == "failed")
            // Information, not Warning — deliberately, so don't flip this back on the next review.
            // Warning would lump every failure type (validation, NotFound, a cancelled request) into
            // one noisy "top problems" bucket. The exception itself is already logged at Error by the
            // global exception handler; top problems there groups by message template PLUS exception
            // type (see GetLogSummaryQueryHandler), so a NotFoundException and a SqlException land in
            // separate rows despite sharing the handler's one constant template. This line only needs
            // to record the outcome and timing.
            logger.LogInformation(template, name, outcome, ms, request);
        else if (elapsed.TotalSeconds > 3)
            logger.LogWarning(template, name, outcome, ms, request);
        else if (isQuery)
            logger.LogDebug(template, name, outcome, ms, request);
        else
            logger.LogInformation(template, name, outcome, ms, request);
    }
}
