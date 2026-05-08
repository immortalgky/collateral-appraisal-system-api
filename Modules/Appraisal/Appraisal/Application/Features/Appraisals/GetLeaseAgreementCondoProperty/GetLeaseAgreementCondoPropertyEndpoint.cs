using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetLeaseAgreementCondoProperty;

public class GetLeaseAgreementCondoPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/lease-agreement-condo-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetLeaseAgreementCondoPropertyQuery(appraisalId, propertyId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetLeaseAgreementCondoPropertyResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetLeaseAgreementCondoProperty")
            .Produces<GetLeaseAgreementCondoPropertyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get lease agreement condo property detail")
            .WithDescription("Retrieves a lease agreement condo property with its detail by property ID.")
            .WithTags("Appraisal Properties");
    }
}
