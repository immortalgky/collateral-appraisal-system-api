using Carter;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Notification.Notification.Features.GetUserNotifications;

public class GetUserNotificationsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/notifications/{userId}", GetUserNotifications)
            .WithName("GetUserNotifications")
            .WithSummary("Get user notifications")
            .WithDescription("Retrieves notifications for a specific user")
            .Produces<GetUserNotificationsResponse>()
            .RequireAuthorization();

        app.MapGet("/api/notifications/{userId}/unread", GetUnreadNotifications)
            .WithName("GetUnreadNotifications")
            .WithSummary("Get unread user notifications")
            .WithDescription("Retrieves unread notifications for a specific user")
            .Produces<GetUserNotificationsResponse>()
            .RequireAuthorization();
    }

    private static async Task<IResult> GetUserNotifications(
        string userId,
        [FromQuery] bool unreadOnly,
        ISender sender)
    {
        var query = new GetUserNotificationsQuery(userId, unreadOnly);
        var result = await sender.Send(query);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetUnreadNotifications(
        string userId,
        ISender sender)
    {
        var query = new GetUserNotificationsQuery(userId, true);
        var result = await sender.Send(query);

        return Results.Ok(result);
    }
}