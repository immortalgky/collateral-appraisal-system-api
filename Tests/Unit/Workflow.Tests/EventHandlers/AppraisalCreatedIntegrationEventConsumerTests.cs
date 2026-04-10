using FluentAssertions;
using MassTransit;
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
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IWorkflowUnitOfWork _unitOfWork;
    private readonly AppraisalCreatedIntegrationEventConsumer _sut;

    public AppraisalCreatedIntegrationEventConsumerTests()
    {
        var logger = Substitute.For<ILogger<AppraisalCreatedIntegrationEventConsumer>>();
        _instanceRepository = Substitute.For<IWorkflowInstanceRepository>();
        _unitOfWork = Substitute.For<IWorkflowUnitOfWork>();
        var inboxGuard = Substitute.For<InboxGuard<WorkflowDbContext>>(
            Substitute.For<WorkflowDbContext>(),
            Substitute.For<ILogger<InboxGuard<WorkflowDbContext>>>());

        // InboxGuard returns false (= proceed with processing)
        inboxGuard.TryClaimAsync(Arg.Any<Guid?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _sut = new AppraisalCreatedIntegrationEventConsumer(
            logger, _instanceRepository, _unitOfWork, inboxGuard);
    }

    [Fact]
    public async Task Consume_WorkflowExists_SetsAppraisalIdInVariables()
    {
        var requestId = Guid.NewGuid();
        var appraisalId = Guid.NewGuid();
        var instance = WorkflowInstance.Create(
            Guid.NewGuid(), "test-workflow", requestId.ToString(), "system");

        _instanceRepository.GetByCorrelationId(requestId.ToString(), Arg.Any<CancellationToken>())
            .Returns(instance);

        var ctx = BuildContext(new AppraisalCreatedIntegrationEvent
        {
            AppraisalId = appraisalId,
            RequestId = requestId,
            AppraisalNumber = "APR-001",
            AppraisalType = "Initial",
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow
        });

        await _sut.Consume(ctx);

        instance.Variables["appraisalId"].Should().Be(appraisalId);
        instance.Variables["appraisalNumber"].Should().Be("APR-001");
        instance.Variables["appraisalType"].Should().Be("Initial");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_NoWorkflowFound_DoesNotThrow()
    {
        var requestId = Guid.NewGuid();

        _instanceRepository.GetByCorrelationId(requestId.ToString(), Arg.Any<CancellationToken>())
            .Returns((WorkflowInstance?)null);

        var ctx = BuildContext(new AppraisalCreatedIntegrationEvent
        {
            AppraisalId = Guid.NewGuid(),
            RequestId = requestId,
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow
        });

        await _sut.Consume(ctx);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
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
