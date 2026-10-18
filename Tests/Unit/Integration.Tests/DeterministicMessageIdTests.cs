using FluentAssertions;
using Integration.FailedMessages;

namespace Integration.Tests;

/// <summary>
/// An Unparseable row has no real MassTransit MessageId, so
/// <see cref="FailedMessageCollectorService.DeterministicMessageId"/> synthesizes one from the broker
/// message's own content — the same broker message (same queue/kind/body) must always hash to the same
/// id, or a redelivery after a lost ack inserts a fresh duplicate row instead of hitting the dedup index.
/// </summary>
public class DeterministicMessageIdTests
{
    [Fact]
    public void SameSourceQueueKindAndBody_ProducesTheSameId()
    {
        var body = "{\"foo\":\"bar\"}"u8.ToArray();

        var first = FailedMessageCollectorService.DeterministicMessageId("appraisal-sync", "Error", body);
        var second = FailedMessageCollectorService.DeterministicMessageId("appraisal-sync", "Error", body);

        first.Should().Be(second);
    }

    [Fact]
    public void DifferentBody_ProducesADifferentId()
    {
        var id1 = FailedMessageCollectorService.DeterministicMessageId(
            "appraisal-sync", "Error", "{\"foo\":\"bar\"}"u8.ToArray());
        var id2 = FailedMessageCollectorService.DeterministicMessageId(
            "appraisal-sync", "Error", "{\"foo\":\"baz\"}"u8.ToArray());

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void DifferentSourceQueueOrKind_ProducesADifferentId_EvenWithTheSameBody()
    {
        var body = "{\"foo\":\"bar\"}"u8.ToArray();

        var byQueue = FailedMessageCollectorService.DeterministicMessageId("appraisal-sync", "Error", body);
        var byOtherQueue = FailedMessageCollectorService.DeterministicMessageId("appraisal-status-dashboard", "Error", body);
        var byOtherKind = FailedMessageCollectorService.DeterministicMessageId("appraisal-sync", "Skipped", body);

        byQueue.Should().NotBe(byOtherQueue);
        byQueue.Should().NotBe(byOtherKind);
    }

    [Fact]
    public void SameBody_DifferentHeaders_ProducesADifferentId_ButHeaderOrderDoesNotMatter()
    {
        var body = Array.Empty<byte>();
        var a = new Dictionary<string, string> { ["x"] = "1", ["y"] = "2" };
        var aReordered = new Dictionary<string, string> { ["y"] = "2", ["x"] = "1" };
        var b = new Dictionary<string, string> { ["x"] = "1", ["y"] = "3" };

        var idA = FailedMessageCollectorService.DeterministicMessageId("q", "Error", body, a);

        idA.Should().Be(FailedMessageCollectorService.DeterministicMessageId("q", "Error", body, aReordered));
        idA.Should().NotBe(FailedMessageCollectorService.DeterministicMessageId("q", "Error", body, b));
    }
}
