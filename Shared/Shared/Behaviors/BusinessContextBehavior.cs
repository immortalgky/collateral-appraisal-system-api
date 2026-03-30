using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using MediatR;
using Serilog.Context;

namespace Shared.Behaviors;

public class BusinessContextBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
    where TResponse : notnull
{
    // Well-known business ID property names to extract
    private static readonly string[] TrackedProperties =
    [
        "Id",
        "AppraisalId",
        "RequestId",
        "WorkflowInstanceId",
        "CollateralId",
        "DocumentId"
    ];

    // Cache property lookups per request type
    private static readonly ConcurrentDictionary<Type, (PropertyInfo Prop, string LogName, string TagName)[]> PropertyCache = new();

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var properties = PropertyCache.GetOrAdd(
            typeof(TRequest),
            type =>
            {
                var requestName = type.Name
                    .Replace("Command", "")
                    .Replace("Query", "");

                // Derive module name from namespace
                // e.g. "Request.Application.Features..." -> "Request"
                //      "Appraisal.Application.Features..." -> "Appraisal"
                var module = type.Namespace?.Split('.').FirstOrDefault() ?? "Unknown";

                return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => TrackedProperties.Contains(p.Name) && p.PropertyType == typeof(Guid))
                    .Select(p =>
                    {
                        var (logName, tagName) = p.Name switch
                        {
                            // When "Id", use the module name: Request.Id -> RequestId / request.id
                            "Id" => ($"{module}Id", $"{module.ToLowerInvariant()}.id"),
                            "AppraisalId" => ("AppraisalId", "appraisal.id"),
                            "RequestId" => ("RequestId", "request.id"),
                            "WorkflowInstanceId" => ("WorkflowInstanceId", "workflow.instance.id"),
                            "CollateralId" => ("CollateralId", "collateral.id"),
                            "DocumentId" => ("DocumentId", "document.id"),
                            _ => (p.Name, p.Name.ToLowerInvariant())
                        };
                        return (Prop: p, LogName: logName, TagName: tagName);
                    })
                    .ToArray();
            });

        if (properties.Length == 0)
            return await next(cancellationToken);

        // Build disposable stack for Serilog log context
        var disposables = new List<IDisposable>(properties.Length);

        try
        {
            foreach (var (prop, logName, tagName) in properties)
            {
                var value = (Guid)prop.GetValue(request)!;
                if (value == Guid.Empty) continue;

                var stringValue = value.ToString();

                // Enrich Serilog log context
                disposables.Add(LogContext.PushProperty(logName, stringValue));

                // Also add generic EntityId so you can always search by it
                if (logName != "EntityId")
                    disposables.Add(LogContext.PushProperty("EntityId", stringValue));

                // Enrich current trace span (OpenTelemetry)
                Activity.Current?.SetTag(tagName, stringValue);
                Activity.Current?.SetTag("entity.id", stringValue);
            }

            return await next(cancellationToken);
        }
        finally
        {
            foreach (var d in disposables)
                d.Dispose();
        }
    }
}
