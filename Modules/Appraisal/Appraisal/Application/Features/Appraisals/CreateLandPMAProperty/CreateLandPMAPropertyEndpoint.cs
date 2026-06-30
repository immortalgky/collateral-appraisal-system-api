namespace Appraisal.Application.Features.Appraisals.CreateLandProperty;

public class CreateLandPMAPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/land-pma",
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

                    var response = result.Adapt<CreateLandPropertyResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/properties/{response.PropertyId}/land-detail",
                        response);
                }
            )
            .WithName("CreateLandProperty")
            .Produces<CreateLandPropertyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a land property")
            .WithDescription("Creates a new land property with its appraisal detail for an appraisal.")
            .WithTags("Appraisal Properties");
    }
}