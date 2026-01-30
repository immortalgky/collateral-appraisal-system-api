using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.AddPropertyToGroup;

public class AddPropertyToGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId}/property-groups/{groupId}/properties",
                async (
                    Guid appraisalId,
                    Guid groupId,
                    AddPropertyToGroupRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new AddPropertyToGroupCommand(
                        appraisalId,
                        groupId,
                        request.PropertyId
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<AddPropertyToGroupResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("AddPropertyToGroup")
            .Produces<AddPropertyToGroupResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Add property to group")
            .WithDescription("Add a property to a property group.")
            .WithTags("Appraisal");
    }
}
