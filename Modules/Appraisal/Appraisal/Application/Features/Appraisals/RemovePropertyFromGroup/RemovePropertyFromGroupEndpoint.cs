using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.RemovePropertyFromGroup;

public class RemovePropertyFromGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/appraisals/{appraisalId}/property-groups/{groupId}/properties/{propertyId}",
                async (
                    Guid appraisalId,
                    Guid groupId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new RemovePropertyFromGroupCommand(
                        appraisalId,
                        groupId,
                        propertyId
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<RemovePropertyFromGroupResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("RemovePropertyFromGroup")
            .Produces<RemovePropertyFromGroupResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Remove property from group")
            .WithDescription("Remove a property from a property group.")
            .WithTags("Appraisal");
    }
}
