using FluentAssertions;
using Integration.Application.Features.OutboxMessages;
using Integration.Application.Features.OutboxMessages.GetOutboxMessages;

namespace Integration.Tests;

/// <summary>
/// The outbox list pages over ids only, then fetches display columns for the page's rows. A row can be purged or
/// change between those two queries; the merge must still return one item per page row (the page reports the id
/// query's count), and must trust the page-time Status so a row listed under the Failed tab never shows Pending.
/// </summary>
public class GetOutboxMessagesMergeTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 12, 0, 0);

    private static GetOutboxMessagesQueryHandler.OutboxDetailRow Detail(
        Guid id, string? error = null, string payload = "{}") =>
        new(id, "Some.Event, Nowhere", "corr-1", null, payload, error, null, 3);

    [Fact]
    public void BuildItems_RowMissingFromDetailFetch_StillAppearsAndPageSizeIsKept()
    {
        var a = Guid.CreateVersion7();
        var gone = Guid.CreateVersion7();
        var c = Guid.CreateVersion7();
        var occurred = Now.AddMinutes(-5);
        GetOutboxMessagesQueryHandler.OutboxPageRow[] page =
        [
            new("request", a, occurred, "Failed"),
            new("request", gone, occurred, "Failed"),
            new("appraisal", c, occurred, "Failed")
        ];
        var details = new Dictionary<(string, Guid), GetOutboxMessagesQueryHandler.OutboxDetailRow>
        {
            [("request", a)] = Detail(a, "boom"),
            [("appraisal", c)] = Detail(c, "boom")
        };

        var items = GetOutboxMessagesQueryHandler.BuildItems(page, details, new Dictionary<(string, Guid), int>(), Now);

        items.Should().HaveCount(page.Length);
        items.Select(i => i.Id).Should().Equal([a, gone, c], "page order is kept");
        var missing = items[1];
        missing.Module.Should().Be("request");
        missing.OccurredAt.Should().Be(occurred);
        missing.Status.Should().Be("Failed");
        missing.EventType.Should().BeEmpty();
        missing.CorrelationId.Should().BeNull();
        missing.Error.Should().BeNull();
        missing.ProcessedAt.Should().BeNull();
        missing.NewerSentCount.Should().BeNull("with no CorrelationId read, 'none newer' is unknown, not 0");
        missing.TypeResolvable.Should().BeFalse();
        missing.FailureClass.Should().BeNull();
    }

    [Fact]
    public void BuildItems_AllRowsMissingFromDetailFetch_PageIsNotEmpty()
    {
        var id = Guid.CreateVersion7();
        GetOutboxMessagesQueryHandler.OutboxPageRow[] page = [new("workflow", id, Now.AddDays(-1), "Pending")];

        var items = GetOutboxMessagesQueryHandler.BuildItems(
            page, new Dictionary<(string, Guid), GetOutboxMessagesQueryHandler.OutboxDetailRow>(),
            new Dictionary<(string, Guid), int>(), Now);

        items.Should().ContainSingle().Which.Status.Should().Be("Pending");
    }

    /// <summary>The detail fetch no longer reads Status at all, so a row listed as Failed (then resent before the
    /// detail read) keeps the status it was listed under, and is classified against that status.</summary>
    [Fact]
    public void BuildItems_UsesPageTimeStatus_ForStatusAndFailureClass()
    {
        var id = Guid.CreateVersion7();
        GetOutboxMessagesQueryHandler.OutboxPageRow[] page = [new("request", id, Now.AddMinutes(-1), "Failed")];
        var details = new Dictionary<(string, Guid), GetOutboxMessagesQueryHandler.OutboxDetailRow>
        {
            [("request", id)] = Detail(id, "Unresolvable type: Some.Event, Nowhere")
        };

        var item = GetOutboxMessagesQueryHandler.BuildItems(
            page, details, new Dictionary<(string, Guid), int>(), Now).Single();

        item.Status.Should().Be("Failed");
        item.FailureClass.Should().Be(OutboxFailureClass.Unresolvable);
    }
}
