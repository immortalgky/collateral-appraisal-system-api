using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.EventHandlers;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Xunit;

namespace Workflow.Tests.EventHandlers;

public class AppraisalCreatedIntegrationEventConsumerTests
{
    private static WorkflowDbContext NewDb() =>
        new(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"ac-link-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task Consume_WorkflowExists_SetsAppraisalIdInVariables()
    {
        await using var db = NewDb();
        var instanceRepository = Substitute.For<IWorkflowInstanceRepository>();
        var unitOfWork = Substitute.For<IWorkflowUnitOfWork>();
        var inboxGuard = new InboxGuard<WorkflowDbContext>(
            db, Substitute.For<ILogger<InboxGuard<WorkflowDbContext>>>());

        var requestId = Guid.NewGuid();
        var appraisalId = Guid.NewGuid();
        var instance = WorkflowInstance.Create(
            Guid.NewGuid(), "test-workflow", requestId.ToString(), "system");

        instanceRepository.GetByCorrelationId(requestId.ToString(), Arg.Any<CancellationToken>())
            .Returns(instance);

        var consumer = new AppraisalCreatedIntegrationEventConsumer(
            Substitute.For<ILogger<AppraisalCreatedIntegrationEventConsumer>>(),
            instanceRepository, unitOfWork, inboxGuard);

        var ctx = BuildContext(new AppraisalCreatedIntegrationEvent
        {
            AppraisalId = appraisalId,
            RequestId = requestId,
            AppraisalNumber = "APR-001",
            AppraisalType = "Initial",
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow
        });

        await consumer.Consume(ctx);

        instance.Variables["appraisalId"].Should().Be(appraisalId);
        instance.Variables["appraisalNumber"].Should().Be("APR-001");
        instance.Variables["appraisalType"].Should().Be("Initial");
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_NoWorkflowFound_MarksProcessedAndDoesNotSave()
    {
        await using var db = NewDb();
        var instanceRepository = Substitute.For<IWorkflowInstanceRepository>();
        var unitOfWork = Substitute.For<IWorkflowUnitOfWork>();
        var inboxGuard = new InboxGuard<WorkflowDbContext>(
            db, Substitute.For<ILogger<InboxGuard<WorkflowDbContext>>>());

        var requestId = Guid.NewGuid();

        instanceRepository.GetByCorrelationId(requestId.ToString(), Arg.Any<CancellationToken>())
            .Returns((WorkflowInstance?)null);

        var consumer = new AppraisalCreatedIntegrationEventConsumer(
            Substitute.For<ILogger<AppraisalCreatedIntegrationEventConsumer>>(),
            instanceRepository, unitOfWork, inboxGuard);

        var ctx = BuildContext(new AppraisalCreatedIntegrationEvent
        {
            AppraisalId = Guid.NewGuid(),
            RequestId = requestId,
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow
        });

        await consumer.Consume(ctx);

        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Helpers ──

    private static ConsumeContext<AppraisalCreatedIntegrationEvent> BuildContext(
        AppraisalCreatedIntegrationEvent message)
    {
        var ctx = Substitute.For<ConsumeContext<AppraisalCreatedIntegrationEvent>>();
        ctx.Message.Returns(message);
        ctx.MessageId.Returns(Guid.NewGuid());
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }
}
