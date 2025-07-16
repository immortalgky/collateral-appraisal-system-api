namespace Assignment.Assignments.Features.UpdateAssignment
{
    public class UpdateAssignmentEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPut("/assigments/{id:long}",
                    async (long id, UpdateAssignmentRequest request, ISender sender, CancellationToken cancellationToken) =>
                    {
                        var command = request.Adapt<UpdateAssignmentCommand>() with { Id = id };

                        var result = await sender.Send(command, cancellationToken);

                        var response = result.Adapt<UpdateAssignmentResult>();

                        return Results.Ok(response);
                    })
                .WithName("UpdateAssignment")
                .Produces<UpdateAssignmentResponse>()
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .WithSummary("Update an existing request")
                .WithDescription(
                    "Updates an existing request in the system. The request details are provided in the request body.");
        }
    }
}