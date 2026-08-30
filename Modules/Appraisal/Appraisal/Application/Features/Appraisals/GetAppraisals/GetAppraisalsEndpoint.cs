using Carter;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisals;

public class GetAppraisalsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals",
                async (
                    [AsParameters] PaginationRequest pagination,
                    [AsParameters] AppraisalListQueryParams queryParams,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var filter = queryParams.ToFilterRequest();

                    var query = new GetAppraisalsQuery(pagination, filter);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(new GetAppraisalsResponse(result.Result, result.Facets));
                }
            )
            .WithName("GetAppraisals")
            .Produces<GetAppraisalsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get all appraisals")
            .WithDescription(
                "Retrieves all appraisals with pagination, filtering, sorting, and facet counts. " +
                "Supports text search (search), multi-value filters (comma-separated status, priority, appraisalType, slaStatus, assignmentType, purpose, propertyType), " +
                "date ranges (createdFrom/To, slaDueDateFrom/To, assignedDateFrom/To, appointmentDateFrom/To), " +
                "geographic filters (province, district), and sorting (sortBy, sortDir). " +
                "propertyType matches appraisals having at least one property of the given type(s).")
            .WithTags("Appraisal");
    }
}
