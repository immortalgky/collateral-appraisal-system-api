using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.UpdatePropertyGroup;

public class UpdatePropertyGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId}/property-groups/{groupId}",
                async (
                    Guid appraisalId,
                    Guid groupId,
                    UpdatePropertyGroupRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UpdatePropertyGroupCommand(
                        appraisalId,
                        groupId,
                        request.GroupName,
                        request.Description,
                        request.UseSystemCalc
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UpdatePropertyGroupResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("UpdatePropertyGroup")
            .Produces<UpdatePropertyGroupResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update property group")
            .WithDescription("Update an existing property group.")
            .WithTags("Appraisal");
    }
}
