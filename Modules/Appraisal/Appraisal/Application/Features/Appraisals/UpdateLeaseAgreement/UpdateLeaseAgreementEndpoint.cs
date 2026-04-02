namespace Appraisal.Application.Features.Appraisals.UpdateLeaseAgreement;

public class UpdateLeaseAgreementEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/lease-agreement",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    UpdateLeaseAgreementRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<UpdateLeaseAgreementCommand>()
                        with { AppraisalId = appraisalId, PropertyId = propertyId };

                    await sender.Send(command, cancellationToken);

                    return Results.NoContent();
                }
            )
            .WithName("UpdateLeaseAgreement")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update lease agreement detail")
            .WithDescription("Update the lease agreement detail for a lease agreement property.")
            .WithTags("Appraisal Properties");
    }
}
