using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.GetCalendar;

public class GetCalendarEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/calendar",
                async (
                    string? month,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrWhiteSpace(month))
                        return Results.BadRequest("The 'month' query parameter is required (format: YYYY-MM).");

                    // Validate format before dispatching to handler
                    if (!System.Text.RegularExpressions.Regex.IsMatch(month, @"^\d{4}-(?:0[1-9]|1[0-2])$"))
                        return Results.BadRequest("Invalid month format. Expected YYYY-MM (e.g. 2026-04).");

                    var result = await sender.Send(new GetCalendarQuery(month), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetCalendar")
            .Produces<GetCalendarResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Get aggregated calendar events (meetings, task due dates, SLA deadlines) for the current user in a given month")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}
