using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Dashboard.GetCalendar;

public class GetCalendarEndpoint : ICarterModule
{
    private static readonly HashSet<string> KnownTypes =
        new(StringComparer.OrdinalIgnoreCase) { "meeting", "task_due" };

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/calendar",
                async (
                    string? month,
                    DateOnly? from,
                    DateOnly? to,
                    string? type,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    DateOnly resolvedFrom;
                    DateOnly resolvedTo;

                    var hasRange = from.HasValue && to.HasValue;
                    var hasMonth = !string.IsNullOrWhiteSpace(month);

                    if (hasRange && hasMonth)
                        return Results.Problem(
                            "Provide either 'month' or 'from'+'to', not both.",
                            statusCode: 400);

                    if (!hasRange && !hasMonth)
                        return Results.Problem(
                            "Either 'month' (format: YYYY-MM) or both 'from' and 'to' must be supplied.",
                            statusCode: 400);

                    if (hasMonth)
                    {
                        // Back-compat shim: derive range from the month string.
                        if (!System.Text.RegularExpressions.Regex.IsMatch(
                                month!, @"^\d{4}-(?:0[1-9]|1[0-2])$",
                                System.Text.RegularExpressions.RegexOptions.None,
                                TimeSpan.FromSeconds(1)))
                            return Results.Problem(
                                "Invalid month format. Expected YYYY-MM (e.g. 2026-04).",
                                statusCode: 400);

                        var firstOfMonth = DateOnly.ParseExact(
                            month! + "-01", "yyyy-MM-dd",
                            System.Globalization.CultureInfo.InvariantCulture);
                        resolvedFrom = firstOfMonth;
                        resolvedTo = firstOfMonth.AddMonths(1).AddDays(-1);
                    }
                    else
                    {
                        if (from!.Value > to!.Value)
                            return Results.Problem(
                                "'from' must not be later than 'to'.",
                                statusCode: 400);

                        resolvedFrom = from.Value;
                        resolvedTo = to.Value;
                    }

                    // Validate type CSV when provided.
                    if (!string.IsNullOrWhiteSpace(type))
                    {
                        var parts = type.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        var unknowns = parts.Where(p => !KnownTypes.Contains(p)).ToList();
                        if (unknowns.Count > 0)
                            return Results.Problem(
                                $"Unknown type(s): {string.Join(", ", unknowns)}. Allowed values: meeting, task_due.",
                                statusCode: 400);
                    }

                    var result = await sender.Send(
                        new GetCalendarQuery(resolvedFrom, resolvedTo, type),
                        cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetCalendar")
            .Produces<GetCalendarResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Get aggregated calendar events (meetings, task due dates) for the current user in a given date range. Task rows include an IsSlaCritical flag when the task's SLA is AtRisk/Breached so the UI can render urgency.")
            .WithTags("Dashboard")
            .RequireAuthorization();
    }
}
