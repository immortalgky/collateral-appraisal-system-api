using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.Data;
using Notification.Domain.Notifications.EventHandlers;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using NSubstitute;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Notification.Tests.EventHandlers;

public class QuotationAutoAssignNotificationHandlerTests
{
    [Fact]
    public async Task Incomplete_run_is_always_sent_as_QuotationAutoAssignIncomplete_to_the_recipient()
    {
        var notifications = Substitute.For<INotificationService>();
        // MessageId is null below, so the real InboxGuard never touches the provider-less context.
        var inbox = new InboxGuard<NotificationDbContext>(
            new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>().Options),
            NullLogger<InboxGuard<NotificationDbContext>>.Instance,
            Substitute.For<IDateTimeProvider>());
        var handler = new QuotationAutoAssignNotificationIntegrationEventHandler(
            notifications, inbox, NullLogger<QuotationAutoAssignNotificationIntegrationEventHandler>.Instance);
        var quotationId = Guid.NewGuid();
        var ctx = Substitute.For<ConsumeContext<QuotationAutoAssignNotificationIntegrationEvent>>();
        ctx.Message.Returns(new QuotationAutoAssignNotificationIntegrationEvent
        {
            QuotationRequestId = quotationId,
            Recipient = "P0001",
            Title = "Quotation auto-assign incomplete",
            Message = "1 of 2 assigned",
            ActionUrl = $"/quotations/{quotationId}"
        });
        ctx.MessageId.Returns((Guid?)null);
        ctx.CancellationToken.Returns(CancellationToken.None);

        await handler.Consume(ctx);

        await notifications.Received(1).SendNotificationToUserAsync(
            "P0001", "Quotation auto-assign incomplete", "1 of 2 assigned",
            NotificationType.QuotationAutoAssignIncomplete, $"/quotations/{quotationId}",
            Arg.Is<Dictionary<string, object>>(m => (Guid)m["quotationRequestId"] == quotationId));
    }
}
