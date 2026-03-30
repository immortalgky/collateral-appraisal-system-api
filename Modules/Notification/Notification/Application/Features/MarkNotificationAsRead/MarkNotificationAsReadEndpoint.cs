using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Notification.Domain.Notifications.Features.MarkNotificationAsRead;

public class MarkNotificationAsReadEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/notifications/{notificationId:guid}/read", MarkAsRead)
            .WithName("MarkNotificationAsRead")
            .WithSummary("Mark notification as read")
            .WithDescription("Marks a specific notification as read")
            .Produces<MarkNotificationAsReadResponse>();

        app.MapPatch("/notifications/users/{username}/read-all", MarkAllAsRead)
            .WithName("MarkAllNotificationsAsRead")
            .WithSummary("Mark all notifications as read")
            .WithDescription("Marks all notifications for a user as read")
            .Produces<MarkNotificationAsReadResponse>();
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
        string username,
        ISender sender)
    {
        var command = new MarkNotificationAsReadCommand(null, username);
        var result = await sender.Send(command);

        return Results.Ok(result);
    }
}