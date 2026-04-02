namespace Appraisal.Application.Features.Appraisals.CreateLeaseAgreementLandAndBuildingProperty;

public class CreateLeaseAgreementLandAndBuildingPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/lease-agreement-land-and-building-properties",
                async (
                    Guid appraisalId,
                    Guid? groupId,
                    CreateLeaseAgreementLandAndBuildingPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateLeaseAgreementLandAndBuildingPropertyCommand>()
                        with
                        {
                            AppraisalId = appraisalId,
                            GroupId = groupId
                        };

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateLeaseAgreementLandAndBuildingPropertyResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/properties/{response.PropertyId}/lease-agreement-land-and-building-detail",
                        response);
                }
            )
            .WithName("CreateLeaseAgreementLandAndBuildingProperty")
            .Produces<CreateLeaseAgreementLandAndBuildingPropertyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create a lease agreement land and building property")
            .WithDescription("Creates a new lease agreement land and building property with both details, lease agreement, and rental info.")
            .WithTags("Appraisal Properties");
    }
}
