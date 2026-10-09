using FluentAssertions;
using Integration.Domain.FailedMessages;

namespace Integration.Tests;

/// <summary>
/// Discard accepts Pending OR RetryRequested; RevertRetry is the collector's way out of a
/// RetryRequested row whose republish came back unroutable. Pure domain-level invariants, no I/O.
/// </summary>
public class FailedMessageDomainTests
{
    private static FailedMessage CreatePending() =>
        FailedMessage.Create(
            "APP-NODE-01", "appraisal-sync", FailedMessageKind.Error, Guid.NewGuid(), null, "Test.FakeEvent",
            null, "System.TimeoutException", "boom", null, 0, DateTime.Now, DateTime.Now, null, null, null,
            "{}"u8.ToArray(), "application/json", null);

    [Fact]
    public void Discard_PendingRow_Succeeds()
    {
        var message = CreatePending();

        message.Discard("actor", "reason", DateTime.Now).Should().BeTrue();
        message.Status.Should().Be(FailedMessageStatus.Discarded);
    }

    [Fact]
    public void Discard_RetryRequestedRow_Succeeds()
    {
        var message = CreatePending();
        message.RequestRetry("actor", null, DateTime.Now).Should().BeTrue();

        message.Discard("actor", "node is dead, clearing it", DateTime.Now).Should().BeTrue();
        message.Status.Should().Be(FailedMessageStatus.Discarded);
    }

    [Fact]
    public void Discard_AlreadyRetried_Fails()
    {
        var message = CreatePending();
        message.RequestRetry("actor", null, DateTime.Now);
        message.MarkRetried(DateTime.Now);

        message.Discard("actor", "reason", DateTime.Now).Should().BeFalse();
        message.Status.Should().Be(FailedMessageStatus.Retried);
    }

    [Fact]
    public void RevertRetry_FromRetryRequested_MovesBackToPendingWithReason()
    {
        var message = CreatePending();
        message.RequestRetry("actor", null, DateTime.Now).Should().BeTrue();
        var now = DateTime.Now;

        message.RevertRetry("Retry failed: queue not found", now).Should().BeTrue();

        message.Status.Should().Be(FailedMessageStatus.Pending);
        message.ActionReason.Should().Be("Retry failed: queue not found");
        message.ActionAt.Should().Be(now);
    }

    /// <summary>
    /// RevertRetry must NOT overwrite ActionBy with "system" — it stays the
    /// actor who originally requested the retry. The system-generated nature of this failure is recorded
    /// separately, in the RetryFailed audit row (ActorCode null there, per the contract).
    /// </summary>
    [Fact]
    public void RevertRetry_KeepsOriginalRequesterAsActionBy()
    {
        var message = CreatePending();
        message.RequestRetry("jsmith", "please retry", DateTime.Now).Should().BeTrue();

        message.RevertRetry("Retry failed: queue not found", DateTime.Now).Should().BeTrue();

        message.ActionBy.Should().Be("jsmith");
    }

    /// <summary>
    /// MarkRetried stamps ActionAt to NOW (when the retry actually resolved) — not left at
    /// whatever RequestRetry set it to (when the retry was only requested). The summary's
    /// last24h.retried and the 90-day cleanup both key off ActionAt and mean "resolved in the window".
    /// </summary>
    [Fact]
    public void MarkRetried_MovesActionAtToResolutionTime_KeepsOriginalActionBy()
    {
        var message = CreatePending();
        var requestedAt = new DateTime(2026, 1, 1, 9, 0, 0);
        message.RequestRetry("jsmith", null, requestedAt).Should().BeTrue();

        var resolvedAt = requestedAt.AddMinutes(10);
        message.MarkRetried(resolvedAt).Should().BeTrue();

        message.Status.Should().Be(FailedMessageStatus.Retried);
        message.ActionAt.Should().Be(resolvedAt);
        message.ActionBy.Should().Be("jsmith");
    }

    [Fact]
    public void RevertRetry_FromPending_Fails_RowStaysAsIs()
    {
        // A transient publish failure never calls RevertRetry — the row simply stays RetryRequested
        // (see FailedMessageCollectorService.RetryAsync's generic catch). RevertRetry itself only ever
        // applies to a row that IS RetryRequested; anything else is a no-op guard.
        var message = CreatePending();

        message.RevertRetry("some reason", DateTime.Now).Should().BeFalse();
        message.Status.Should().Be(FailedMessageStatus.Pending);
    }

    [Fact]
    public void FailedMessageStatus_All_ListsEveryStatusConstant()
    {
        // A literal array (typed string[]), exactly the four statuses, in declaration order.
        string[] all = FailedMessageStatus.All;
        all.Should().Equal("Pending", "RetryRequested", "Retried", "Discarded");
    }

    /// <summary>The literal array is hand-maintained, so a status constant added without joining it fails here.</summary>
    [Fact]
    public void FailedMessageStatus_All_ContainsEveryPublicStringConstant()
    {
        var constants = typeof(FailedMessageStatus)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

        FailedMessageStatus.All.Should().BeEquivalentTo(constants);
    }
}
