namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementCondoProperty;

public class UpdateLeaseAgreementCondoPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/lease-agreement-condo-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    UpdateLeaseAgreementCondoPropertyRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateLeaseAgreementCondoPropertyCommand>()
                        with
                        {
                            AppraisalId = appraisalId, PropertyId = propertyId
                        };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateLeaseAgreementCondoProperty")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update lease agreement condo property detail")
            .WithDescription("Update the detail of a lease agreement condo property.")
            .WithTags("Appraisal Properties");
    }
}
