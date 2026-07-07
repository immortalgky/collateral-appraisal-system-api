
public class CreateCondoPMAPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/condo-pma",
                async (
                    Guid appraisalId,
                    Guid? groupId,
                    CreateCondoPMAPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateCondoPMAPropertyCommand>()
                        with
                        {
                            AppraisalId = appraisalId, GroupId = groupId
                        };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateCondoPMAPropertyResponse>();


                    return Results.Created(
                        $"/appraisals/{appraisalId}/properties-pma/{response.PropertyId}/condo-pma",
                        response);
                }
            )
            .WithName("CreateCondoPMAProperty")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create condo pma property detail")
            .WithDescription("Create the detail of a condo pma property.")
            .WithTags("Appraisal Properties");
    }
}