using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.MovePropertyToGroup;

public class MovePropertyToGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId}/properties/{propertyId}/move-to-group",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    MovePropertyToGroupRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new MovePropertyToGroupCommand(
                        appraisalId,
                        propertyId,
                        request.TargetGroupId,
                        request.TargetPosition
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<MovePropertyToGroupResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("MovePropertyToGroup")
            .Produces<MovePropertyToGroupResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Move property to another group")
            .WithDescription("Move a property from its current group to a different group at an optional position.")
            .WithTags("Appraisal");
    }
}
