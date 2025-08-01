using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Request.Requests.EventHandlers;
using Request.Requests.Events;
using Request.Tests.TestData;
using Shared.Messaging.Events;

namespace Request.Tests.Request.Requests.EventHandlers;

public class RequestCreatedEventHandlerTests
{
    [Fact]
    public async Task Handle_Notification_ShouldPublishIntegrationEvent()
    {
        var logger = Substitute.For<ILogger<RequestCreatedEventHandler>>();
        var bus = Substitute.For<IBus>();
        var handler = new RequestCreatedEventHandler(logger, bus);
        var notification = new RequestCreatedEvent(ModelsTestData.RequestGeneral());

        await handler.Handle(notification, CancellationToken.None);

        await bus.Received(1).Publish(
            Arg.Is<RequestCreatedIntegrationEvent>(e => e.RequestId == notification.Request.Id),
            Arg.Any<CancellationToken>()
        );
    }
}