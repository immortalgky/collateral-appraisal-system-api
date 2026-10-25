using FluentAssertions;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Parameter.Contracts.Parameters;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Workflow.Data;
using Workflow.Data.Repository;
using Workflow.EventHandlers;
using Workflow.Tasks.EventHandlers;
using Workflow.Tasks.Models;
using Workflow.Workflow.Events;
using Workflow.Workflow.Services;

namespace Workflow.Tests.EventHandlers;

/// <summary>
/// A completed pool task must tell the rest of its pool, via the outbox (post-commit), and a
/// personal task must not. The consumer then pushes PoolTaskCompleted.
/// </summary>
public class PoolTaskCompletedNotificationTests
{
    [Theory]
    [InlineData("2", true)]
    [InlineData("1", false)]
    public async Task Handle_PublishesPoolTaskCompletedOnlyForPoolTasks(string assignedType, bool expected)
    {
        var task = PendingTask.Create(Guid.NewGuid(), "Verify", "IntAppraisalVerifier", assignedType,
            DateTime.Now.AddHours(-1), Guid.NewGuid(), "int-appraisal-verification");
        var repo = Substitute.For<IAssignmentRepository>();
        repo.GetPendingTaskAsync(task.CorrelationId, task.TaskName, Arg.Any<CancellationToken>()).Returns(task);
        var outbox = Substitute.For<IIntegrationEventOutbox>();
        var db = new WorkflowDbContext(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var handler = new TaskCompletedDomainEventHandler(repo, Substitute.For<IPublishEndpoint>(), outbox, db,
            Substitute.For<ISender>(), Substitute.For<IParameterLookupService>(),
            Substitute.For<ILogger<TaskCompletedDomainEventHandler>>());

        await handler.Handle(
            new TaskCompletedDomainEvent(task.CorrelationId, task.TaskName, "P", DateTime.Now, "alice"),
            CancellationToken.None);

        outbox.Received(expected ? 1 : 0).Publish(
            Arg.Is<PoolTaskCompletedIntegrationEvent>(e =>
                e.PoolGroup == "IntAppraisalVerifier" && e.TaskId == task.Id && e.CompletedBy == "alice"),
            Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task Consumer_NotifiesPoolTaskCompleted_AndSwallowsPushFailure()
    {
        var notifier = Substitute.For<IWorkflowNotificationService>();
        var taskId = Guid.NewGuid();
        var context = Substitute.For<ConsumeContext<PoolTaskCompletedIntegrationEvent>>();
        context.Message.Returns(new PoolTaskCompletedIntegrationEvent
            { PoolGroup = "G", TaskId = taskId, CompletedBy = "alice" });
        var consumer = new PoolTaskCompletedIntegrationEventConsumer(notifier,
            Substitute.For<ILogger<PoolTaskCompletedIntegrationEventConsumer>>());

        await consumer.Consume(context);
        await notifier.Received(1).NotifyPoolTaskCompleted("G", taskId, "alice");

        notifier.NotifyPoolTaskCompleted(default!, default, default!).ReturnsForAnyArgs<Task>(_ => throw new InvalidOperationException("hub down"));
        var act = () => consumer.Consume(context);
        await act.Should().NotThrowAsync();
    }
}
