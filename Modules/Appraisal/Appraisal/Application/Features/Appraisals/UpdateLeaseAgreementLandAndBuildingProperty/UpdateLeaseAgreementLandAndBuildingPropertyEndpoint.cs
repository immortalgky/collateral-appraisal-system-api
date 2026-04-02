namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementLandAndBuildingProperty;

public class UpdateLeaseAgreementLandAndBuildingPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/lease-agreement-land-building-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    UpdateLeaseAgreementLandAndBuildingPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateLeaseAgreementLandAndBuildingPropertyCommand>()
                        with
                        {
                            AppraisalId = appraisalId, PropertyId = propertyId
                        };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateLeaseAgreementLandAndBuildingProperty")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update lease agreement land and building property detail")
            .WithDescription("Update the detail of a lease agreement land and building property.")
            .WithTags("Appraisal Properties");
    }
}
