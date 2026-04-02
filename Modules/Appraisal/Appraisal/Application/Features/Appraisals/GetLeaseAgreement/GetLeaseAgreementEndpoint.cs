namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreement;

public class GetLeaseAgreementEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/lease-agreement",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetLeaseAgreementQuery(appraisalId, propertyId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetLeaseAgreementResponse>();
                    return Results.Ok(response);
                }
            )
            .WithName("GetLeaseAgreement")
            .Produces<GetLeaseAgreementResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get lease agreement detail")
            .WithDescription("Retrieves the lease agreement detail for a lease agreement property.")
            .WithTags("Appraisal Properties");
    }
}
