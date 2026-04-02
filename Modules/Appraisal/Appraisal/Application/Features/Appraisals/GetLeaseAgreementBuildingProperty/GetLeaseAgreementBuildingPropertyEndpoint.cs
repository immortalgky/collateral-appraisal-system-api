using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreementBuildingProperty;

public class GetLeaseAgreementBuildingPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/lease-agreement-building-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetLeaseAgreementBuildingPropertyQuery(appraisalId, propertyId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetLeaseAgreementBuildingPropertyResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetLeaseAgreementBuildingProperty")
            .Produces<GetLeaseAgreementBuildingPropertyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get lease agreement building property detail")
            .WithDescription("Retrieves a lease agreement building property with its detail by property ID.")
            .WithTags("Appraisal Properties");
    }
}
