using Carter;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Notification.Domain.Notifications.Features.GetUserNotifications;

public class GetUserNotificationsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/notifications/{username}", GetUserNotifications)
            .WithName("GetUserNotifications")
            .WithSummary("Get user notifications")
            .WithDescription("Retrieves notifications for a specific user")
            .Produces<GetUserNotificationsResponse>();

        app.MapGet("/notifications/{username}/unread", GetUnreadNotifications)
            .WithName("GetUnreadNotifications")
            .WithSummary("Get unread user notifications")
            .WithDescription("Retrieves unread notifications for a specific user")
            .Produces<GetUserNotificationsResponse>();
    }

    private static async Task<IResult> GetUserNotifications(
        string username,
        ISender sender)
    {
        var query = new GetUserNotificationsQuery(username);
        var result = await sender.Send(query);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetUnreadNotifications(
        string username,
        ISender sender)
    {
        var query = new GetUserNotificationsQuery(username, true);
        var result = await sender.Send(query);

        return Results.Ok(result);
    }
}