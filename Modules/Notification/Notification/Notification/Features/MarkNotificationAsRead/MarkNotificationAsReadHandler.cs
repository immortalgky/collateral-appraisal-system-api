using Notification.Notification.Services;
using Shared.Contracts.CQRS;

namespace Notification.Notification.Features.MarkNotificationAsRead;

public class MarkNotificationAsReadHandler : ICommandHandler<MarkNotificationAsReadCommand, MarkNotificationAsReadResponse>
{
    private readonly INotificationService _notificationService;

    public MarkNotificationAsReadHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<MarkNotificationAsReadResponse> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        if (request.NotificationId.HasValue)
        {
            await _notificationService.MarkNotificationAsReadAsync(request.NotificationId.Value);
            return new MarkNotificationAsReadResponse(true, "Notification marked as read");
        }
        
        if (!string.IsNullOrEmpty(request.UserId))
        {
            await _notificationService.MarkAllNotificationsAsReadAsync(request.UserId);
            return new MarkNotificationAsReadResponse(true, "All notifications marked as read");
        }

        return new MarkNotificationAsReadResponse(false, "Invalid request parameters");
    }
}