using Appraisal.Application.EventHandlers;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.EventHandlers;

public class InternalFollowupAssignedIntegrationEventHandlerTests
{
    [Fact]
    public async Task Consume_AttachesTheFollowupToTheNewestLiveRow_NotToAnOlderAssignedOne()
    {
        // The same election as the other assignment consumers (AppraisalAssignmentElection): newest by creation.
        // The old rule ordered by AssignedAt first, so an older, already-assigned row beat the newer Pending one.
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "Initial", "Normal", DateTime.Now);
        var older = appraisal.AssignAdmin();
        older.CreatedAt = new DateTime(2026, 1, 1);
        older.Assign("Internal", assigneeUserId: "P0001", assignedBy: "P5229");
        var newer = appraisal.AssignAdmin();
        newer.CreatedAt = new DateTime(2026, 2, 1);
        Assert.NotNull(older.AssignedAt);
        Assert.Null(newer.AssignedAt);

        await BuildHandler(appraisal).Consume(BuildContext(new InternalFollowupAssignedIntegrationEvent
        {
            AppraisalId = appraisal.Id,
            InternalAppraiserId = "P0002",
            InternalFollowupAssignmentMethod = "manual"
        }));

        Assert.Equal("P0002", newer.InternalAppraiserId);
        Assert.Null(older.InternalAppraiserId);
    }

    [Fact]
    public async Task Consume_IgnoresCancelledRows()
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "Initial", "Normal", DateTime.Now);
        var live = appraisal.AssignAdmin();
        live.CreatedAt = new DateTime(2026, 1, 1);
        var cancelled = appraisal.AssignAdmin();
        cancelled.CreatedAt = new DateTime(2026, 2, 1);
        cancelled.Cancel("test");

        await BuildHandler(appraisal).Consume(BuildContext(new InternalFollowupAssignedIntegrationEvent
        {
            AppraisalId = appraisal.Id,
            InternalAppraiserId = "P0002",
            InternalFollowupAssignmentMethod = "manual"
        }));

        Assert.Equal("P0002", live.InternalAppraiserId);
        Assert.Null(cancelled.InternalAppraiserId);
    }

    private static InternalFollowupAssignedIntegrationEventHandler BuildHandler(AppraisalAggregate appraisal)
    {
        var repo = Substitute.For<IAppraisalRepository>();
        repo.GetByIdWithAllDataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(appraisal);
        // Provider-less context: BuildContext sets a null MessageId, so the InboxGuard never touches it.
        var db = new AppraisalDbContext(new DbContextOptionsBuilder<AppraisalDbContext>().Options);
        var inboxGuard = new InboxGuard<AppraisalDbContext>(
            db, NullLogger<InboxGuard<AppraisalDbContext>>.Instance, Substitute.For<IDateTimeProvider>());

        return new InternalFollowupAssignedIntegrationEventHandler(
            repo, Substitute.For<IAppraisalUnitOfWork>(),
            NullLogger<InternalFollowupAssignedIntegrationEventHandler>.Instance, inboxGuard);
    }

    private static ConsumeContext<InternalFollowupAssignedIntegrationEvent> BuildContext(
        InternalFollowupAssignedIntegrationEvent message)
    {
        var ctx = Substitute.For<ConsumeContext<InternalFollowupAssignedIntegrationEvent>>();
        ctx.Message.Returns(message);
        ctx.MessageId.Returns((Guid?)null);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }
}
