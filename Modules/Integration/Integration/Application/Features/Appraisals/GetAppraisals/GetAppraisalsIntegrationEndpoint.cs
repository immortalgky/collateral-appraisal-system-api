using Appraisal.Application.Features.Appraisals.GetAppraisals;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Integration.Application.Features.Appraisals.GetAppraisals;

// Mirrors legacy GET /appraisals for LOS (Appraisal/Application/Features/Appraisals/GetAppraisals/GetAppraisalsEndpoint.cs).
public class GetAppraisalsIntegrationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/appraisals", async (
                [AsParameters] PaginationRequest pagination,
                [AsParameters] AppraisalListQueryParams queryParams,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var filter = queryParams.ToFilterRequest();

                var query = new GetAppraisalsQuery(pagination, filter);

                var result = await sender.Send(query, cancellationToken);

                return Results.Ok(new GetAppraisalsResponse(result.Result, result.Facets));
            })
            .WithName("Integration_GetAppraisals")
            .WithTags("Integration - Appraisals")
            .Produces<GetAppraisalsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("Integration");
    }
}
