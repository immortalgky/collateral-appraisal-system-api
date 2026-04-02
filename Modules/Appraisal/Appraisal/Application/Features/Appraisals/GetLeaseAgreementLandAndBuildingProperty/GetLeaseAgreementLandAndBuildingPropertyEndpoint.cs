using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreementLandAndBuildingProperty;

public class GetLeaseAgreementLandAndBuildingPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/lease-agreement-land-building-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetLeaseAgreementLandAndBuildingPropertyQuery(appraisalId, propertyId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetLeaseAgreementLandAndBuildingPropertyResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetLeaseAgreementLandAndBuildingProperty")
            .Produces<GetLeaseAgreementLandAndBuildingPropertyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get lease agreement land and building property detail")
            .WithDescription("Retrieves a lease agreement land and building property with its detail by property ID.")
            .WithTags("Appraisal Properties");
    }
}
