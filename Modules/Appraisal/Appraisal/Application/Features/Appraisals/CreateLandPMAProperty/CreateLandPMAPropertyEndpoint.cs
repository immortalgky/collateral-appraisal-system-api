namespace Appraisal.Application.Features.Appraisals.CreateLandProperty;

public class CreateLandPMAPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/land-building-pma",
                async (
                    Guid appraisalId,
                    Guid? groupId,
                    CreateLandPMAPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateLandPMAPropertyCommand>()
                        with
                        {
                            AppraisalId = appraisalId,
                            GroupId = groupId
                        };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateLandPMAPropertyResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/properties-pma/{response.PropertyId}/land-building-pma",
                        response);
                }
            )
            .WithName("CreateLandPMAProperty")
            .Produces<CreateLandPMAPropertyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a land pma property")
            .WithDescription("Creates a new land pma property with its appraisal detail for an appraisal.")
            .WithTags("Appraisal Properties");
    }
}