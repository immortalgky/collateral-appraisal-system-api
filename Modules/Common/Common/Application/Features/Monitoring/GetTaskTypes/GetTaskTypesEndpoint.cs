using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Application.Features.Monitoring.GetTaskTypes;

public class GetTaskTypesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/task-types",
                async (string? monitoringType, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetTaskTypesQuery(MonitoringTypes.Normalize(monitoringType));
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetTaskTypes")
            .Produces<IReadOnlyList<TaskTypeOption>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Task types")
            .WithDescription("Returns the task types present on the given monitoring screen (monitoringType=Internal|External, default Internal), scoped to the caller's monitoring permissions. Used to populate the taskType filter.")
            .WithTags("Monitoring")
            .RequireAuthorization();
    }
}
