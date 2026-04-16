using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.GetReminders;

public class GetRemindersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/reminders",
                async (
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetRemindersQuery(), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetReminders")
            .Produces<GetRemindersResponse>()
            .WithSummary("Get auto-derived reminders for the current user (overdue/near-due tasks + open follow-ups)")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}
