using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Notification.Notification.Features.MarkNotificationAsRead;

public class MarkNotificationAsReadEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/notifications/{notificationId:guid}/read", MarkAsRead)
            .WithName("MarkNotificationAsRead")
            .WithSummary("Mark notification as read")
            .WithDescription("Marks a specific notification as read")
            .Produces<MarkNotificationAsReadResponse>()
            .RequireAuthorization();

        app.MapPatch("/api/notifications/users/{userId}/read-all", MarkAllAsRead)
            .WithName("MarkAllNotificationsAsRead")
            .WithSummary("Mark all notifications as read")
            .WithDescription("Marks all notifications for a user as read")
            .Produces<MarkNotificationAsReadResponse>()
            .RequireAuthorization();
    }

    private static async Task<IResult> MarkAsRead(
        Guid notificationId,
        ISender sender)
    {
        var command = new MarkNotificationAsReadCommand(notificationId, null);
        var result = await sender.Send(command);

        return Results.Ok(result);
    }

    private static async Task<IResult> MarkAllAsRead(
        string userId,
        ISender sender)
    {
        var command = new MarkNotificationAsReadCommand(null, userId);
        var result = await sender.Send(command);

        return Results.Ok(result);
    }
}