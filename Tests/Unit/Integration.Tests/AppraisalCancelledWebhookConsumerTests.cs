using System.Text.Json;
using FluentAssertions;
using Integration.Application.EventHandlers.Outbound;
using Integration.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Messaging.Events;

namespace Integration.Tests;

public class AppraisalCancelledWebhookConsumerTests
{
    private static async Task<JsonElement> ConsumeAndCaptureData(string? cancelReason)
    {
        var requestId = Guid.NewGuid();
        var webhook = Substitute.For<IWebhookService>();
        var captured = new List<object>();
        webhook.SendAsync(
            eventId: Arg.Any<Guid>(),
            systemCode: Arg.Any<string>(),
            eventType: Arg.Any<string>(),
            externalCaseKey: Arg.Any<string>(),
            occurredAt: Arg.Any<DateTime>(),
            data: Arg.Do<object>(d => captured.Add(d)),
            cancellationToken: Arg.Any<CancellationToken>(),
            wrapInEnvelope: Arg.Any<bool>());
        var lookup = Substitute.For<IAppraisalLookupService>();
        lookup.GetKeysByRequestIdAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(new AppraisalKeys("APR-001", "CASE-1", "LOS"));

        var context = Substitute.For<ConsumeContext<AppraisalCancelIntegrationEvent>>();
        context.Message.Returns(new AppraisalCancelIntegrationEvent
        {
            CorrelationId = requestId,
            CancelledBy = "user1",
            CancelledAt = DateTime.Now,
            CancelReason = cancelReason
        });
        context.CancellationToken.Returns(CancellationToken.None);

        await new AppraisalCancelledWebhookConsumer(
            webhook, lookup, NullLogger<AppraisalCancelledWebhookConsumer>.Instance).Consume(context);

        captured.Should().ContainSingle();
        return JsonSerializer.SerializeToElement(captured[0]);
    }

    [Fact]
    public async Task CancelReason_IsSentAsReason()
    {
        var data = await ConsumeAndCaptureData("Customer withdrew the request");

        data.GetProperty("appraisalNumber").GetString().Should().Be("APR-001");
        data.GetProperty("reason").GetString().Should().Be("Customer withdrew the request");
    }

    [Fact]
    public async Task NullCancelReason_IsSentAsEmptyString()
    {
        var data = await ConsumeAndCaptureData(null);

        data.GetProperty("reason").GetString().Should().BeEmpty();
    }
}
