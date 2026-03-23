namespace Parameter.ConstructionWork.Features.GetConstructionWorkGroups;

public class GetConstructionWorkGroupsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/construction-work-groups",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetConstructionWorkGroupsQuery(), cancellationToken);
                    return Results.Ok(result.Groups);
                })
            .WithName("GetConstructionWorkGroups")
            .Produces<List<ConstructionWorkGroupDto>>(StatusCodes.Status200OK)
            .WithSummary("Get construction work groups")
            .WithDescription("Retrieve all construction work groups with their predefined work items.")
            .WithTags("Parameter");
    }
}
