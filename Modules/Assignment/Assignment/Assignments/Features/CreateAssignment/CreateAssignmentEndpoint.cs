namespace Assignment.Assignments.Features.CreateAssignment;

public class CreateAssignmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/assignments",
                async (CreateAssignmentRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    // var command = request.Adapt<CreateAssignmentCommand>();
                    var command = new CreateAssignmentCommand(
                            request.RequestId,
                            request.AssignmentMethod,
                            request.ExternalCompanyId,
                            request.ExternalCompanyAssignType,
                            request.ExtApprStaff,
                            request.ExtApprStaffAssignmentType,
                            request.IntApprStaff,
                            request.IntApprStaffAssignmentType,
                            request.Remark
                        );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateAssignmentResponse>();

                    return Results.Created($"/assignments/{response.Id}", response);
                })
            .WithName("CreateAssignment")
            .Produces<CreateAssignmentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create a new Assignment")
            .WithDescription(
                "Creates a new assignment in the system. The request assignment are provided in the request body.");
    }
}