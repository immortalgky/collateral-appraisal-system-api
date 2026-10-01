using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Logs.GetLogById;

public class GetLogByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/admin/logs/{id:long}",
                async (long id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetLogByIdQuery(id), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("AdminGetLogById")
            .Produces<LogDetailDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Admin: Get a single log entry")
            .WithDescription("Returns the full row from dbo.Logs, including the raw Properties JSON, for the detail drawer's Properties tab. Requires LOGS_VIEW permission.")
            .WithTags("Logs")
            .RequireAuthorization("LogsView");
    }
}
