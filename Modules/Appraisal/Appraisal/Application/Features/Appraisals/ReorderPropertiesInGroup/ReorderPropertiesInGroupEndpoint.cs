using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.ReorderPropertiesInGroup;

public class ReorderPropertiesInGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId}/property-groups/{groupId}/reorder-properties",
                async (
                    Guid appraisalId,
                    Guid groupId,
                    ReorderPropertiesInGroupRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new ReorderPropertiesInGroupCommand(
                        appraisalId,
                        groupId,
                        request.OrderedPropertyIds
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<ReorderPropertiesInGroupResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("ReorderPropertiesInGroup")
            .Produces<ReorderPropertiesInGroupResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Reorder properties in group")
            .WithDescription("Reorder properties within a group by providing the full ordered list of property IDs.")
            .WithTags("Appraisal");
    }
}
