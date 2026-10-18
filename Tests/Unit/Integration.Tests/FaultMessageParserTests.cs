using System.Globalization;
using System.Text;
using FluentAssertions;
using Integration.Domain.FailedMessages;
using Integration.FailedMessages;
using RabbitMQ.Client;

namespace Integration.Tests;

/// <summary>
/// Header sets below were captured verbatim from a real MassTransit 8.4.1 + RabbitMQ.Client 7.1.2
/// round-trip (publish → consumer throws → inspect the resulting <c>_error</c>/<c>_skipped</c>
/// message) — see the implementation report for how they were produced.
/// </summary>
public class FaultMessageParserTests
{
    private static readonly byte[] EnvelopeBody = Encoding.UTF8.GetBytes("""
        {
          "messageId": "1fbc0000-caae-ab74-be6a-08df1bf84509",
          "conversationId": "1fbc0000-caae-ab74-d3d6-08df1bf8450a",
          "messageType": ["urn:message:Probe:TestFail"],
          "message": { "appraisalId": "8f2c1111-2222-3333-4444-555566667777", "name": "hello" }
        }
        """);

    [Fact]
    public void Parse_ErrorHeaders_ExtractsFaultInfoFromMtFaultHeaders()
    {
        var headers = new Dictionary<string, string>
        {
            ["MT-Fault-ExceptionType"] = "System.InvalidOperationException",
            ["MT-Fault-Message"] = "boom from test consumer",
            ["MT-Fault-StackTrace"] = "   at Probe.FailingConsumer.Consume(ConsumeContext`1 context)",
            ["MT-Fault-MessageType"] = "Probe.TestFail",
            ["MT-Fault-ConsumerType"] = "Probe.FailingConsumer",
            ["MT-Fault-RetryCount"] = "2",
            ["MT-Fault-Timestamp"] = "2026-09-26T18:02:03.5080760Z",
            ["MT-Reason"] = "fault"
        };

        var result = FaultMessageParser.Parse(headers, EnvelopeBody);

        result.MessageId.Should().Be(Guid.Parse("1fbc0000-caae-ab74-be6a-08df1bf84509"));
        result.ConversationId.Should().Be(Guid.Parse("1fbc0000-caae-ab74-d3d6-08df1bf8450a"));
        result.MessageType.Should().Be("Probe.TestFail");
        result.ConsumerType.Should().Be("Probe.FailingConsumer");
        result.ExceptionType.Should().Be("System.InvalidOperationException");
        result.ExceptionMessage.Should().Be("boom from test consumer");
        result.RetryCount.Should().Be(2);
        result.FaultedAtUtc.Should().Be(DateTime.Parse("2026-09-26T18:02:03.5080760Z",
            CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal));
        result.MessagePayloadJson.Should().Contain("appraisalId");
    }

    [Fact]
    public void Parse_SkippedHeaders_HasNoFaultHeaders_UsesMtReasonAsMessage()
    {
        // A real _skipped message: MT-Reason is "dead-letter", not "No consumer" — MassTransit never
        // writes that literal string; it's only this app's fallback for a missing MT-Reason header.
        var headers = new Dictionary<string, string> { ["MT-Reason"] = "dead-letter" };

        var result = FaultMessageParser.Parse(headers, EnvelopeBody);

        result.ExceptionType.Should().Be(FaultMessageParser.SkippedExceptionType);
        result.ExceptionMessage.Should().Be("dead-letter");
        result.ConsumerType.Should().BeNull();
        result.RetryCount.Should().Be(0);
        result.FaultedAtUtc.Should().BeNull();
        result.MessageType.Should().Be("Probe.TestFail"); // derived from envelope messageType[0]
    }

    [Fact]
    public void Parse_SkippedHeaders_MissingMtReason_DefaultsToNoConsumer()
    {
        var result = FaultMessageParser.Parse(new Dictionary<string, string>(), ReadOnlyMemory<byte>.Empty);

        result.ExceptionType.Should().Be(FaultMessageParser.SkippedExceptionType);
        result.ExceptionMessage.Should().Be("No consumer");
    }

    [Fact]
    public void Parse_SkippedHeaders_EmptyMtReason_DefaultsToNoConsumer()
    {
        // Realistic broker data: the header key is present but its value is an empty string.
        var headers = new Dictionary<string, string> { ["MT-Reason"] = "" };

        var result = FaultMessageParser.Parse(headers, ReadOnlyMemory<byte>.Empty);

        result.ExceptionType.Should().Be(FaultMessageParser.SkippedExceptionType);
        result.ExceptionMessage.Should().Be("No consumer");
    }

    [Fact]
    public void Parse_ErrorHeaders_EmptyMtFaultMessage_FallsBackToExceptionType()
    {
        // Realistic broker data: some exceptions have no Message at all, so MT-Fault-Message can be "".
        var headers = new Dictionary<string, string>
        {
            ["MT-Fault-ExceptionType"] = "System.InvalidOperationException",
            ["MT-Fault-Message"] = ""
        };

        var result = FaultMessageParser.Parse(headers, ReadOnlyMemory<byte>.Empty);

        result.ExceptionType.Should().Be("System.InvalidOperationException");
        result.ExceptionMessage.Should().Be("System.InvalidOperationException");
    }

    [Fact]
    public void Parse_ErrorHeaders_EmptyExceptionType_FallsBackToPlaceholder()
    {
        var headers = new Dictionary<string, string> { ["MT-Fault-ExceptionType"] = "" };

        var result = FaultMessageParser.Parse(headers, ReadOnlyMemory<byte>.Empty);

        result.ExceptionType.Should().NotBeNullOrWhiteSpace();
        result.ExceptionMessage.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>FailedMessage.Create rejects blank (whitespace-only) values, so a whitespace-only header must
    /// fall back to the placeholder just like an empty one — otherwise BuildEntity throws and a perfectly
    /// good message is stored as Unparseable.</summary>
    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Parse_ErrorHeaders_WhitespaceOnlyFaultMessageAndExceptionType_FallBackAndSatisfyCreate(string blank)
    {
        var withBlankMessage = FaultMessageParser.Parse(new Dictionary<string, string>
        {
            ["MT-Fault-ExceptionType"] = "System.InvalidOperationException",
            ["MT-Fault-Message"] = blank
        }, ReadOnlyMemory<byte>.Empty);

        withBlankMessage.ExceptionMessage.Should().Be("System.InvalidOperationException");

        var withBlankType = FaultMessageParser.Parse(new Dictionary<string, string>
        {
            ["MT-Fault-ExceptionType"] = blank,
            ["MT-Fault-Message"] = "boom"
        }, ReadOnlyMemory<byte>.Empty);

        withBlankType.ExceptionType.Should().Be("System.Exception");

        // The real consumer of these values: Create must not throw on any of them.
        var create = () => FailedMessage.Create(
            "APP-NODE-01", "appraisal-sync", FailedMessageKind.Error, Guid.NewGuid(), null,
            withBlankType.MessageType, null, withBlankType.ExceptionType, withBlankMessage.ExceptionMessage,
            null, 0, DateTime.Now, DateTime.Now, null, null, null,
            "{}"u8.ToArray(), "application/json", null);
        create.Should().NotThrow();
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Parse_SkippedHeaders_WhitespaceOnlyMtReason_DefaultsToNoConsumer(string blank)
    {
        var result = FaultMessageParser.Parse(
            new Dictionary<string, string> { ["MT-Reason"] = blank }, ReadOnlyMemory<byte>.Empty);

        result.ExceptionMessage.Should().Be("No consumer");
    }

    [Fact]
    public void Parse_EnvelopeWithNumericMessageId_DoesNotThrow_MessageIdIsNull()
    {
        var body = Encoding.UTF8.GetBytes("""
            {
              "messageId": 12345,
              "conversationId": "1fbc0000-caae-ab74-d3d6-08df1bf8450a",
              "messageType": [999],
              "message": { "name": "hello" }
            }
            """);

        var result = FaultMessageParser.Parse(new Dictionary<string, string>(), body);

        result.MessageId.Should().BeNull();
        result.ConversationId.Should().Be(Guid.Parse("1fbc0000-caae-ab74-d3d6-08df1bf8450a"));
        result.MessageType.Should().Be("Unknown"); // messageType[0] wasn't a string either
    }

    [Theory]
    [InlineData("appraisal-sync_error", "appraisal-sync", FailedMessageKind.Error)]
    [InlineData("appraisal-sync_skipped", "appraisal-sync", FailedMessageKind.Skipped)]
    [InlineData("appraisal-sync", "appraisal-sync", null)]
    public void FromFaultQueue_SplitsBaseNameAndKind(string queueName, string expectedSourceQueue, string? expectedKind)
    {
        var (sourceQueue, kind) = FaultMessageParser.FromFaultQueue(queueName);

        sourceQueue.Should().Be(expectedSourceQueue);
        kind.Should().Be(expectedKind);
    }

    [Fact]
    public void CoerceHeaders_ConvertsByteArrayToUtf8String()
    {
        var headers = new Dictionary<string, object?> { ["X"] = Encoding.UTF8.GetBytes("hello") };

        FaultMessageParser.CoerceHeaders(headers)["X"].Should().Be("hello");
    }

    [Fact]
    public void CoerceHeaders_ConvertsAmqpTimestampToUnixSeconds()
    {
        var headers = new Dictionary<string, object?> { ["ts"] = new AmqpTimestamp(1234567890) };

        FaultMessageParser.CoerceHeaders(headers)["ts"].Should().Be("1234567890");
    }

    [Fact]
    public void CoerceHeaders_ConvertsLongToString()
    {
        var headers = new Dictionary<string, object?> { ["seq"] = 42L };

        FaultMessageParser.CoerceHeaders(headers)["seq"].Should().Be("42");
    }

    [Fact]
    public void CoerceHeaders_ConvertsNestedListToJsonText()
    {
        var headers = new Dictionary<string, object?>
        {
            ["list"] = new List<object?> { "a", 1, Encoding.UTF8.GetBytes("b") }
        };

        FaultMessageParser.CoerceHeaders(headers)["list"].Should().Be("""["a","1","b"]""");
    }

    [Theory]
    [InlineData("appraisal-sync_error", "appraisal-sync")]
    [InlineData("appraisal-sync_skipped", "appraisal-sync")]
    [InlineData("appraisal-sync", "appraisal-sync")]
    public void StripFaultSuffix_RemovesTrailingErrorOrSkippedSuffix(string sourceQueue, string expected) =>
        FaultMessageParser.StripFaultSuffix(sourceQueue).Should().Be(expected);

    [Theory]
    [InlineData("appraisal-sync_error", FailedMessageKind.Error)]
    [InlineData("appraisal-sync_skipped", FailedMessageKind.Skipped)]
    [InlineData("appraisal-sync", null)]
    public void DeriveKind_ReadsQueueSuffix(string queueName, string? expectedKind) =>
        FaultMessageParser.DeriveKind(queueName).Should().Be(expectedKind);
}
