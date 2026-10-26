using FluentAssertions;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Appraisal.Contracts.Appraisals;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.Data.Entities;
using Workflow.EventHandlers;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Engine.Expression;
using Workflow.Workflow.Models;
using Workflow.Workflow.Pipeline;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;
using Xunit;

namespace Workflow.Tests.EventHandlers;

/// <summary>
/// A construction-inspection (Progressive) request copies the appraisal the user PICKED. The chain tip is still
/// resolved for the company (Progressive forces it, Appeal excludes it) but no longer replaces the pick as the
/// copy base — the deferred path (EmitAppraisalCreationRequestedStep) has always sent the pick.
/// </summary>
public class RequestSubmittedIntegrationEventConsumerTests
{
    private static readonly Guid Picked = Guid.NewGuid();
    private static readonly Guid Tip = Guid.NewGuid();
    private static readonly Guid TipCompany = Guid.NewGuid();
    private static readonly Guid PickedCompany = Guid.NewGuid();

    private sealed class Harness : IAsyncDisposable
    {
        public readonly WorkflowDbContext Db = new(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"rs-consumer-{Guid.NewGuid()}").Options);
        public readonly IWorkflowInstanceRepository Instances = Substitute.For<IWorkflowInstanceRepository>();
        public readonly IWorkflowService Workflows = Substitute.For<IWorkflowService>();
        public readonly IIntegrationEventOutbox Outbox = Substitute.For<IIntegrationEventOutbox>();
        public readonly ISender Mediator = Substitute.For<ISender>();
        public readonly IWorkflowUnitOfWork UnitOfWork = Substitute.For<IWorkflowUnitOfWork>();
        public readonly RequestSubmittedIntegrationEventConsumer Consumer;
        public Dictionary<string, object>? StartedWith;

        public Harness()
        {
            var definitions = Substitute.For<IWorkflowDefinitionRepository>();
            var definition = WorkflowDefinition.Create("Collateral Appraisal Workflow", "d", "{}", "c", "system");
            definitions.GetLatestVersion("Collateral Appraisal Workflow", Arg.Any<CancellationToken>()).Returns(definition);

            // The immediate-emit trigger: one active config row with no condition.
            Db.ActivityProcessConfigurations.Add(ActivityProcessConfiguration.Create(
                "__on_workflow_start__", "emit", "EmitAppraisalCreationRequested", StepKind.Action, 1, "system"));
            Db.SaveChanges();

            Workflows.StartWorkflowAsync(
                    Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, object>?>(),
                    Arg.Any<string?>(), Arg.Any<Dictionary<string, RuntimeOverride>?>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    StartedWith = call.ArgAt<Dictionary<string, object>?>(3);
                    return WorkflowInstance.Create(definition.Id, "i", "c", "system");
                });

            UnitOfWork.CreateExecutionStrategy().Returns(Db.Database.CreateExecutionStrategy());

            var inbox = new InboxGuard<WorkflowDbContext>(
                Db, Substitute.For<ILogger<InboxGuard<WorkflowDbContext>>>(),
                Substitute.For<Shared.Time.IDateTimeProvider>());

            Consumer = new RequestSubmittedIntegrationEventConsumer(
                Substitute.For<ILogger<RequestSubmittedIntegrationEventConsumer>>(),
                definitions, Instances, Workflows, UnitOfWork, Outbox,
                new AppraisalCreationTriggerEvaluator(
                    Db, Substitute.For<IExpressionEvaluator>(),
                    Substitute.For<ILogger<AppraisalCreationTriggerEvaluator>>()),
                Mediator, inbox);
        }

        public Task Consume(string purpose, string appraisalType) => Consumer.Consume(Context(new RequestSubmittedIntegrationEvent
        {
            RequestId = RequestId,
            RequestTitles = [],
            Purpose = purpose,
            AppraisalType = appraisalType,
            PrevAppraisalId = Picked,
            CreatedBy = "tester"
        }));

        public readonly Guid RequestId = Guid.NewGuid();

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private static ConsumeContext<RequestSubmittedIntegrationEvent> Context(RequestSubmittedIntegrationEvent message)
    {
        var ctx = Substitute.For<ConsumeContext<RequestSubmittedIntegrationEvent>>();
        ctx.Message.Returns(message);
        // null skips the inbox bookkeeping, which is raw SQL the InMemory provider cannot run.
        ctx.MessageId.Returns((Guid?)null);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    private static void GivenChainTip(Harness h, Guid? pickedCompany = null, string pickedCompanyName = "") =>
        h.Mediator.Send(Arg.Any<ResolveLatestInAppraisalChainQuery>(), Arg.Any<CancellationToken>())
            .Returns(new AppraisalChainRef(Tip, TipCompany, "Tip Co", 2, pickedCompany, pickedCompanyName));

    private static void AssertPublishedCopyBase(Harness h, Guid expected) =>
        h.Outbox.Received(1).Publish(
            Arg.Is<AppraisalCreationRequestedIntegrationEvent>(e => e.PrevAppraisalId == expected),
            Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>());

    [Theory]
    [InlineData("06")]
    [InlineData("11")]
    public async Task Progressive_copies_the_picked_appraisal_not_the_chain_tip(string purpose)
    {
        await using var h = new Harness();
        GivenChainTip(h);

        await h.Consume(purpose, "Progressive");

        AssertPublishedCopyBase(h, Picked);
    }

    [Fact]
    public async Task Progressive_forces_the_company_of_the_picked_appraisal_not_the_chain_tip()
    {
        await using var h = new Harness();
        GivenChainTip(h, PickedCompany, "Picked Co");

        await h.Consume("06", "Progressive");

        h.StartedWith!["forceCompanyId"].Should().Be(PickedCompany.ToString());
        h.StartedWith["forceCompanyName"].Should().Be("Picked Co");
    }

    [Fact]
    public async Task Progressive_forces_no_company_when_the_pick_was_internal_even_if_the_tip_was_external()
    {
        await using var h = new Harness();
        GivenChainTip(h, pickedCompany: null);

        await h.Consume("11", "Progressive");

        h.StartedWith.Should().NotContainKey("forceCompanyId");
        h.StartedWith.Should().NotContainKey("forceCompanyName");
    }

    [Fact]
    public async Task Appeal_keeps_copying_the_pick_and_excludes_the_company_of_the_chain_tip()
    {
        await using var h = new Harness();
        GivenChainTip(h);

        await h.Consume("12", "New");

        AssertPublishedCopyBase(h, Picked);
        h.StartedWith!["excludedCompanyId"].Should().Be(TipCompany.ToString());
        h.StartedWith.Should().NotContainKey("forceCompanyId");
    }

    [Fact]
    public async Task The_retry_path_republishes_the_picked_appraisal_without_resolving_the_chain()
    {
        await using var h = new Harness();
        var existing = WorkflowInstance.Create(Guid.NewGuid(), "i", h.RequestId.ToString(), "system");
        h.Instances.GetByCorrelationId(h.RequestId.ToString(), Arg.Any<CancellationToken>()).Returns(existing);

        await h.Consume("06", "Progressive");

        AssertPublishedCopyBase(h, Picked);
        await h.Mediator.DidNotReceive().Send(Arg.Any<ResolveLatestInAppraisalChainQuery>(), Arg.Any<CancellationToken>());
    }
}
