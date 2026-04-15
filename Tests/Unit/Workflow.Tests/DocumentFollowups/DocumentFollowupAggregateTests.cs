using FluentAssertions;
using Workflow.DocumentFollowups.Domain;
using Workflow.DocumentFollowups.Domain.Events;
using Xunit;

namespace Workflow.Tests.DocumentFollowups;

public class DocumentFollowupAggregateTests
{
    private static DocumentFollowup CreateOpenFollowup(int items = 2)
    {
        var lineItems = Enumerable.Range(1, items)
            .Select(i => ($"DocType{i}", (string?)$"notes-{i}"));
        return DocumentFollowup.Raise(
            appraisalId: Guid.NewGuid(),
            requestId: Guid.NewGuid(),
            raisingWorkflowInstanceId: Guid.NewGuid(),
            raisingPendingTaskId: Guid.NewGuid(),
            raisingActivityId: "appraisal-initiation-check",
            raisingUserId: "checker-1",
            lineItems: lineItems);
    }

    [Fact]
    public void Raise_WithLineItems_CreatesOpenFollowupAndRaisesEvent()
    {
        var followup = CreateOpenFollowup(3);

        followup.Status.Should().Be(DocumentFollowupStatus.Open);
        followup.LineItems.Should().HaveCount(3);
        followup.LineItems.Should().OnlyContain(li => li.Status == DocumentFollowupLineItemStatus.Pending);
        followup.DomainEvents.Should().ContainSingle(e => e is DocumentFollowupRaisedDomainEvent);
    }

    [Fact]
    public void Raise_WithoutLineItems_Throws()
    {
        Action act = () => DocumentFollowup.Raise(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "act", "user",
            Array.Empty<(string, string?)>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FulfillFirstMatchingByType_MatchesByTypeAndMarksUploaded()
    {
        var followup = CreateOpenFollowup(2);
        var docId = Guid.NewGuid();

        var matchedId = followup.FulfillFirstMatchingByType("DocType1", docId);

        matchedId.Should().NotBeNull();
        var item = followup.LineItems.Single(li => li.Id == matchedId);
        item.Status.Should().Be(DocumentFollowupLineItemStatus.Uploaded);
        item.DocumentId.Should().Be(docId);
        followup.Status.Should().Be(DocumentFollowupStatus.Open); // not all resolved yet
    }

    [Fact]
    public void FulfillFirstMatchingByType_AllItemsFulfilled_StaysOpenUntilExplicitSubmit()
    {
        var followup = CreateOpenFollowup(1);
        followup.ClearDomainEvents();

        followup.FulfillFirstMatchingByType("DocType1", Guid.NewGuid());

        followup.Status.Should().Be(DocumentFollowupStatus.Open);
        followup.DomainEvents.Should().NotContain(e => e is DocumentFollowupResolvedDomainEvent);
    }

    [Fact]
    public void DeclineLineItem_RequiresReason()
    {
        var followup = CreateOpenFollowup(2);
        var firstId = followup.LineItems[0].Id;

        Action act = () => followup.DeclineLineItem(firstId, "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DeclineLineItem_ResolvesItemAndCarriesReason()
    {
        var followup = CreateOpenFollowup(2);
        var first = followup.LineItems[0];

        followup.DeclineLineItem(first.Id, "not available");

        first.Status.Should().Be(DocumentFollowupLineItemStatus.Declined);
        first.Reason.Should().Be("not available");
    }

    [Fact]
    public void Cancel_RequiresReason()
    {
        var followup = CreateOpenFollowup();

        Action act = () => followup.Cancel(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_TransitionsToCancelledAndCancelsAllPending()
    {
        var followup = CreateOpenFollowup(3);
        followup.ClearDomainEvents();

        followup.Cancel("checker withdrew");

        followup.Status.Should().Be(DocumentFollowupStatus.Cancelled);
        followup.CancellationReason.Should().Be("checker withdrew");
        followup.LineItems.Should().OnlyContain(li => li.Status == DocumentFollowupLineItemStatus.Cancelled);
        followup.DomainEvents.Should().ContainSingle(e => e is DocumentFollowupCancelledDomainEvent);
    }

    [Fact]
    public void CancelLineItem_LastPendingStillOpen_AwaitsExplicitSubmit()
    {
        var followup = CreateOpenFollowup(2);
        followup.DeclineLineItem(followup.LineItems[0].Id, "no");
        followup.ClearDomainEvents();

        followup.CancelLineItem(followup.LineItems[1].Id, "obsolete");

        followup.Status.Should().Be(DocumentFollowupStatus.Open);
        followup.DomainEvents.Should().NotContain(e => e is DocumentFollowupResolvedDomainEvent);
    }
}
