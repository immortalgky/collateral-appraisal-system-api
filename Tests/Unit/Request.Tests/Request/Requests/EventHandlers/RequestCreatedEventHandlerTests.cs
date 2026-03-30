using Microsoft.Extensions.Logging;
using NSubstitute;
using Request.Application.EventHandlers.Request;
using Request.Domain.Requests.Events;
using Request.Tests.TestData;
using Shared.Data.Outbox;
using Shared.Messaging.Events;

namespace Request.Tests.Request.Requests.EventHandlers;

public class RequestCreatedEventHandlerTests
{
    [Fact]
    public async Task Handle_Notification_ShouldPublishIntegrationEvent()
    {
        var logger = Substitute.For<ILogger<RequestCreatedEventHandler>>();
        var outbox = Substitute.For<IIntegrationEventOutbox>();
        var handler = new RequestCreatedEventHandler(logger, outbox);
        var notification = new RequestCreatedEvent(ModelsTestData.RequestGeneral());

        await handler.Handle(notification, CancellationToken.None);

        outbox.Received(1).Publish(
            Arg.Is<RequestCreatedIntegrationEvent>(e => e.RequestId == notification.Request.Id),
            Arg.Any<string?>(),
            Arg.Any<Dictionary<string, string>?>()
        );
    }
}
