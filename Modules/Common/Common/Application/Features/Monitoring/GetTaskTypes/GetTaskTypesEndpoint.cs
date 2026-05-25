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
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetTaskTypesQuery(), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetTaskTypes")
            .Produces<IReadOnlyList<TaskTypeOption>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Task types")
            .WithDescription("Returns the universe of all task types defined across active workflow definitions. Used to populate the taskType filter on monitoring screens.")
            .WithTags("Monitoring")
            .RequireAuthorization();
    }
}
