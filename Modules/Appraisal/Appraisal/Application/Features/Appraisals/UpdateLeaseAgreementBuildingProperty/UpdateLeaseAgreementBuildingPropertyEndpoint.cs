namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementBuildingProperty;

public class UpdateLeaseAgreementBuildingPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/lease-agreement-building-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    UpdateLeaseAgreementBuildingPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateLeaseAgreementBuildingPropertyCommand>()
                        with
                        {
                            AppraisalId = appraisalId, PropertyId = propertyId
                        };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateLeaseAgreementBuildingProperty")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update lease agreement building property detail")
            .WithDescription("Update the detail of a lease agreement building property.")
            .WithTags("Appraisal Properties");
    }
}
