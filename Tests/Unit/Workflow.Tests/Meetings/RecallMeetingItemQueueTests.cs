using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Identity;
using Shared.Time;
using Workflow.Data;
using Workflow.Data.Repository;
using Workflow.Meetings.Domain;
using Workflow.Meetings.Features.RecallMeetingItem;
using Workflow.Meetings.Infrastructure;
using Workflow.Meetings.ReadModels;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;

namespace Workflow.Tests.Meetings;

/// <summary>The recall handler's queue branch: a Released queue item is put back whenever one exists.</summary>
public class RecallMeetingItemQueueTests
{
    [Fact]
    public async Task Recall_OnAMeetingReopenedAfterEnding_RequeuesTheReleasedQueueItem()
    {
        // Two items released, meeting ended (queue items Released), then an approver routes B back:
        // the meeting is RoutedBack, A is still Released and so is its queue item.
        var f = await Fixture.CreateAsync();
        f.Meeting.EndAfterVoting(DateTime.Now);
        f.QueueItemA.Release();
        f.Meeting.RecordApproverRouteBack(f.AppraisalB, "jane", "rework", DateTime.Now);
        await f.SaveAsync();
        f.Meeting.Status.Should().Be(MeetingStatus.RoutedBack);

        await f.RecallAAsync();

        (await f.ReloadQueueItemAAsync()).Status.Should().Be(MeetingQueueItemStatus.Assigned);
        (await f.ReloadMeetingAsync()).Status.Should().Be(MeetingStatus.RoutedBack);
    }

    [Fact]
    public async Task Recall_OnAMeetingAwaitingVotes_LeavesTheAssignedQueueItemAlone()
    {
        var f = await Fixture.CreateAsync();
        f.Meeting.Status.Should().Be(MeetingStatus.AwaitingVotes);

        await f.RecallAAsync();

        (await f.ReloadQueueItemAAsync()).Status.Should().Be(MeetingQueueItemStatus.Assigned);
        (await f.ReloadMeetingAsync()).Status.Should().Be(MeetingStatus.InvitationSent);
    }

    private sealed class Fixture
    {
        public required WorkflowDbContext Db { get; init; }
        public required Meeting Meeting { get; init; }
        public required MeetingQueueItem QueueItemA { get; init; }
        public required Guid AppraisalA { get; init; }
        public required Guid AppraisalB { get; init; }
        private Guid _instanceA;

        public static async Task<Fixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<WorkflowDbContext>()
                .UseInMemoryDatabase($"recall-queue-{Guid.NewGuid()}")
                .AddInterceptors(new MeetingRouteBackReturnFlowTests.StampRowVersions()).Options;
            var db = new WorkflowDbContext(options);

            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var instanceA = Guid.NewGuid();
            var meeting = Meeting.Create("M", null, "1/2569", 1, 2569);
            meeting.AddItem(a, "APR-A", 1m, 1m, instanceA, "pending-meeting", DateTime.Now);
            meeting.AddItem(b, "APR-B", 1m, 1m, Guid.NewGuid(), "pending-meeting", DateTime.Now);
            meeting.SendInvitation(DateTime.Now);
            meeting.ReleaseItem(a, "secretary", DateTime.Now);
            meeting.ReleaseItem(b, "secretary", DateTime.Now);   // all released: AwaitingVotes

            var queueItem = MeetingQueueItem.CreateQueued(a, "APR-A", 1m, 1m, instanceA, "pending-meeting");
            queueItem.AssignTo(meeting.Id);

            // The open approval round the recall handler looks for.
            var round = WorkflowActivityExecution.Create(instanceA, "pending-approval", "Approval", "ApprovalActivity");
            round.Start();

            db.Meetings.Add(meeting);
            db.MeetingQueueItems.Add(queueItem);
            db.WorkflowActivityExecutions.Add(round);
            await db.SaveChangesAsync();

            return new Fixture
            {
                Db = db, Meeting = meeting, QueueItemA = queueItem, AppraisalA = a, AppraisalB = b, _instanceA = instanceA
            };
        }

        public Task SaveAsync() => Db.SaveChangesAsync();

        public async Task RecallAAsync()
        {
            var executions = Substitute.For<IWorkflowActivityExecutionRepository>();
            executions.GetCurrentActivityForWorkflowAsync(_instanceA, Arg.Any<CancellationToken>())
                .Returns(await Db.WorkflowActivityExecutions.SingleAsync(e => e.WorkflowInstanceId == _instanceA));
            var persistence = Substitute.For<IWorkflowPersistenceService>();
            persistence.AcquireApplicationLockAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(0);
            var votes = Substitute.For<IApprovalVoteRepository>();
            votes.HasAnyVoteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
            var user = Substitute.For<ICurrentUserService>();
            user.Username.Returns("secretary");
            var clock = Substitute.For<IDateTimeProvider>();
            clock.ApplicationNow.Returns(_ => DateTime.Now);

            var handler = new RecallMeetingItemCommandHandler(
                new MeetingRepository(Db), Db, executions, persistence, votes,
                Substitute.For<IWorkflowService>(), user, clock, NullLogger<RecallMeetingItemCommandHandler>.Instance);

            await handler.Handle(new RecallMeetingItemCommand(Meeting.Id, AppraisalA, "released by mistake", false), default);
        }

        public async Task<MeetingQueueItem> ReloadQueueItemAAsync() =>
            await Db.MeetingQueueItems.AsNoTracking().SingleAsync(q => q.AppraisalId == AppraisalA);

        public async Task<Meeting> ReloadMeetingAsync() =>
            await Db.Meetings.AsNoTracking().SingleAsync(m => m.Id == Meeting.Id);
    }
}
