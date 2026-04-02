namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementLandProperty;

public class UpdateLeaseAgreementLandPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/lease-agreement-land-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    UpdateLeaseAgreementLandPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateLeaseAgreementLandPropertyCommand>()
                        with
                        {
                            AppraisalId = appraisalId, PropertyId = propertyId
                        };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateLeaseAgreementLandProperty")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update lease agreement land property detail")
            .WithDescription("Update the detail of a lease agreement land property.")
            .WithTags("Appraisal Properties");
    }
}
