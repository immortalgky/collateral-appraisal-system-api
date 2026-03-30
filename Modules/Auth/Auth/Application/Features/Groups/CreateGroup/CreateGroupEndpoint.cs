namespace Auth.Application.Features.Groups.CreateGroup;

public class CreateGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/auth/groups",
                async (CreateGroupRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<CreateGroupCommand>();
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<CreateGroupResponse>();
                    return Results.Created($"/auth/groups/{response.Id}", response);
                })
            .WithName("CreateGroup")
            .Produces<CreateGroupResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create a new group")
            .WithDescription("Create a new group with a Bank or Company scope.")
            .WithTags("Group")
            .RequireAuthorization("CanManageGroups");
    }
}
