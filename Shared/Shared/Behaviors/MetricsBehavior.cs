using System.Diagnostics;
using System.Diagnostics.Metrics;
using MediatR;

namespace Shared.Behaviors;

public class MetricsBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
    where TResponse : notnull
{
    private static readonly Meter Meter = new("CAS.MediatR");

    private static readonly Counter<long> RequestsTotal = Meter.CreateCounter<long>(
        "cas_mediatr_requests_total",
        description: "Total MediatR requests handled");

    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "cas_mediatr_duration_seconds", "s",
        "Duration of MediatR request handling");

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken);
            sw.Stop();

            RequestsTotal.Add(1,
                new KeyValuePair<string, object?>("request", requestName),
                new KeyValuePair<string, object?>("status", "success"));
            RequestDuration.Record(sw.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("request", requestName),
                new KeyValuePair<string, object?>("status", "success"));

            return response;
        }
        catch (Exception)
        {
            sw.Stop();

            RequestsTotal.Add(1,
                new KeyValuePair<string, object?>("request", requestName),
                new KeyValuePair<string, object?>("status", "error"));
            RequestDuration.Record(sw.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("request", requestName),
                new KeyValuePair<string, object?>("status", "error"));

            throw;
        }
    }
}
