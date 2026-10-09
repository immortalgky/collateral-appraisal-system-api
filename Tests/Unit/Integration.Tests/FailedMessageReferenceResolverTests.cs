using FluentAssertions;
using Integration.FailedMessages;

namespace Integration.Tests;

public class FailedMessageReferenceResolverTests
{
    [Theory]
    [InlineData("appraisalId", "appraisal")]
    [InlineData("requestId", "request")]
    [InlineData("quotationRequestId", "quotation")]
    [InlineData("meetingId", "meeting")]
    [InlineData("documentId", "document")]
    public void Resolve_FindsEachKnownIdKey(string idKey, string expectedType)
    {
        var id = Guid.NewGuid();
        var json = $$"""{ "{{idKey}}": "{{id}}" }""";

        var (refType, refId, refNumber) = FailedMessageReferenceResolver.Resolve(json);

        refType.Should().Be(expectedType);
        refId.Should().Be(id);
        refNumber.Should().BeNull();
    }

    [Fact]
    public void Resolve_PriorityOrder_AppraisalWinsOverLaterKeys()
    {
        var appraisalId = Guid.NewGuid();
        var json = $$"""
            { "requestId": "{{Guid.NewGuid()}}", "quotationRequestId": "{{Guid.NewGuid()}}",
              "appraisalId": "{{appraisalId}}" }
            """;

        var (refType, refId, _) = FailedMessageReferenceResolver.Resolve(json);

        refType.Should().Be("appraisal");
        refId.Should().Be(appraisalId);
    }

    [Fact]
    public void Resolve_IncludesNumberWhenPresent()
    {
        var appraisalId = Guid.NewGuid();
        var json = $$"""{ "appraisalId": "{{appraisalId}}", "appraisalNumber": "AP-2026-00042" }""";

        var (_, _, refNumber) = FailedMessageReferenceResolver.Resolve(json);

        refNumber.Should().Be("AP-2026-00042");
    }

    [Fact]
    public void Resolve_NeverUsesCorrelationId()
    {
        var json = $$"""{ "correlationId": "{{Guid.NewGuid()}}" }""";

        var (refType, refId, refNumber) = FailedMessageReferenceResolver.Resolve(json);

        refType.Should().BeNull();
        refId.Should().BeNull();
        refNumber.Should().BeNull();
    }

    [Fact]
    public void Resolve_NonJsonBody_ReturnsNulls()
    {
        var (refType, refId, refNumber) = FailedMessageReferenceResolver.Resolve("not json at all");

        refType.Should().BeNull();
        refId.Should().BeNull();
        refNumber.Should().BeNull();
    }

    [Fact]
    public void Resolve_OutboxStyleRawPayload_ResolvesTopLevelKeysDirectly()
    {
        // Outbox Payload has no MassTransit envelope — the event object IS the top-level JSON, same
        // shape this resolver already expects (design: reused as-is by the future API pass).
        var appraisalId = Guid.NewGuid();
        var json = $$"""
            { "appraisalId": "{{appraisalId}}", "appraisalNumber": "AP-2026-00042",
              "occurredAt": "2026-09-27T08:00:00" }
            """;

        var (refType, refId, refNumber) = FailedMessageReferenceResolver.Resolve(json);

        refType.Should().Be("appraisal");
        refId.Should().Be(appraisalId);
        refNumber.Should().Be("AP-2026-00042");
    }
}
