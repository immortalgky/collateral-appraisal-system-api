namespace Auth.Application.Features.Groups.UpdateGroupMonitoring;

public class UpdateGroupMonitoringEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/auth/groups/{id:guid}/monitoring",
                async (Guid id, UpdateGroupMonitoringRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateGroupMonitoringCommand(id, request.MonitoredGroupIds);
                    await sender.Send(command, cancellationToken);
                    return Results.NoContent();
                })
            .WithName("UpdateGroupMonitoring")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update group monitoring")
            .WithDescription("Replace all groups monitored by this group.")
            .WithTags("Group")
            .RequireAuthorization("CanManageGroups");
    }
}
