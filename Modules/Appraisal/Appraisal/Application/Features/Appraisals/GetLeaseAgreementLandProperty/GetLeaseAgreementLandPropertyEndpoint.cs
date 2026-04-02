namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreementLandProperty;

public class GetLeaseAgreementLandPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/lease-agreement-land-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetLeaseAgreementLandPropertyQuery(appraisalId, propertyId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetLeaseAgreementLandPropertyResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetLeaseAgreementLandProperty")
            .Produces<GetLeaseAgreementLandPropertyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get lease agreement land property detail")
            .WithDescription("Retrieves a lease agreement land property with its detail by property ID.")
            .WithTags("Appraisal Properties");
    }
}
