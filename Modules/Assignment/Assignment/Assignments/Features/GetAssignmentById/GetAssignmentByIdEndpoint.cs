namespace Assignment.Assignments.Features.GetAssignmentById;

public class GetAssignmentByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/assignments/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetAssignmentByIdQuery(id), cancellationToken);

            var response = result.Adapt<GetAssignmentByIdResult>();

            return Results.Ok(response);
        })
            .WithName("GetAssignmentById")
            .Produces<GetAssignmentByIdResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get assignment by ID")
            .WithDescription("Get assignment by ID");
    }
}