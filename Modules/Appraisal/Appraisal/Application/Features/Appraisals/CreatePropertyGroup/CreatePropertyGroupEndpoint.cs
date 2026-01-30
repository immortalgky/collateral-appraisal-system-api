using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.CreatePropertyGroup;

public class CreatePropertyGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId}/property-groups",
                async (
                    Guid appraisalId,
                    CreatePropertyGroupRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CreatePropertyGroupCommand(
                        appraisalId,
                        request.GroupName,
                        request.Description
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreatePropertyGroupResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("CreatePropertyGroup")
            .Produces<CreatePropertyGroupResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create property group")
            .WithDescription("Create a new property group within an appraisal.")
            .WithTags("Appraisal");
    }
}
