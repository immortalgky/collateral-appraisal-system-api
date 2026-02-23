using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.DeletePropertyGroup;

public class DeletePropertyGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                "/appraisals/{appraisalId}/property-groups/{groupId}",
                async (
                    Guid appraisalId,
                    Guid groupId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new DeletePropertyGroupCommand(appraisalId, groupId);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<DeletePropertyGroupResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("DeletePropertyGroup")
            .Produces<DeletePropertyGroupResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete property group")
            .WithDescription("Delete a property group from an appraisal.")
            .WithTags("Appraisal");
    }
}
