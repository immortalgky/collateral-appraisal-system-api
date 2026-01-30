using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

public class GetAppraisalsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals",
                async (
                    [AsParameters] PaginationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetAppraisalsQuery(request);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetAppraisalsResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetAppraisals")
            .Produces<GetAppraisalsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get all appraisals")
            .WithDescription("Retrieves all appraisals with pagination support.")
            .WithTags("Appraisal");
    }
}