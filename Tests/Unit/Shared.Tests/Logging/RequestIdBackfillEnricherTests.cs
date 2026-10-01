using FluentAssertions;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Shared.Logging;

namespace Shared.Tests.Logging;

public class RequestIdBackfillEnricherTests
{
    private static readonly RequestIdBackfillEnricher Enricher = new();
    private static readonly FakePropertyFactory PropertyFactory = new();

    [Fact]
    public void Enrich_RequestIdIsGuid_AddsCasRequestId()
    {
        var guid = Guid.NewGuid();
        var logEvent = CreateEvent(new LogEventProperty("RequestId", new ScalarValue(guid)));

        Enricher.Enrich(logEvent, PropertyFactory);

        logEvent.Properties.Should().ContainKey("CasRequestId");
        ((ScalarValue)logEvent.Properties["CasRequestId"]).Value.Should().Be(guid.ToString());
    }

    [Fact]
    public void Enrich_RequestIdIsGuidString_AddsCasRequestId()
    {
        var guid = Guid.NewGuid();
        var logEvent = CreateEvent(new LogEventProperty("RequestId", new ScalarValue(guid.ToString())));

        Enricher.Enrich(logEvent, PropertyFactory);

        logEvent.Properties.Should().ContainKey("CasRequestId");
    }

    [Fact]
    public void Enrich_RequestIdIsHostingConnectionId_DoesNotAddCasRequestId()
    {
        // ASP.NET's own hosting log scope, e.g. "0HNO...:00000001" — not a Guid.
        var logEvent = CreateEvent(new LogEventProperty("RequestId", new ScalarValue("0HNOSKJP49MCB:000000D5")));

        Enricher.Enrich(logEvent, PropertyFactory);

        logEvent.Properties.Should().NotContainKey("CasRequestId");
    }

    [Fact]
    public void Enrich_NoRequestIdProperty_DoesNotAddCasRequestId()
    {
        var logEvent = CreateEvent();

        Enricher.Enrich(logEvent, PropertyFactory);

        logEvent.Properties.Should().NotContainKey("CasRequestId");
    }

    [Fact]
    public void Enrich_CasRequestIdAlreadyPresent_LeavesItUntouched()
    {
        var existing = Guid.NewGuid();
        var otherGuid = Guid.NewGuid();
        var logEvent = CreateEvent(
            new LogEventProperty("CasRequestId", new ScalarValue(existing.ToString())),
            new LogEventProperty("RequestId", new ScalarValue(otherGuid)));

        Enricher.Enrich(logEvent, PropertyFactory);

        ((ScalarValue)logEvent.Properties["CasRequestId"]).Value.Should().Be(existing.ToString());
    }

    private static LogEvent CreateEvent(params LogEventProperty[] properties)
    {
        var template = new MessageTemplateParser().Parse("test");
        return new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, template, properties);
    }

    private sealed class FakePropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }
}
