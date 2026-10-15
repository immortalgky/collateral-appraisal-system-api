using Serilog.Core;
using Serilog.Events;

namespace Shared.Logging;

/// <summary>
/// Many call sites log a real command/query Guid directly as a structured {RequestId} argument
/// (e.g. logger.LogInformation("... {RequestId} ...", requestId)) rather than through
/// BusinessContextBehavior's LogContext push. Since the MSSqlServer sink's RequestId column only
/// reads the CasRequestId property (see BusinessContextBehavior — ASP.NET's own hosting scope also
/// pushes an ambient "RequestId" property, but that one is a connection trace id like
/// "0HN...:00000001", not a Guid), those values were silently lost. This backfills CasRequestId
/// from a "RequestId" property whenever its value is actually a Guid and nothing already set
/// CasRequestId for this event.
/// </summary>
public class RequestIdBackfillEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Properties.ContainsKey("CasRequestId")) return;
        if (!logEvent.Properties.TryGetValue("RequestId", out var value)) return;
        if (value is not ScalarValue scalar) return;

        var guidText = scalar.Value switch
        {
            Guid guid => guid.ToString(),
            string text when Guid.TryParse(text, out _) => text,
            _ => null
        };
        if (guidText is null) return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CasRequestId", guidText));
    }
}
