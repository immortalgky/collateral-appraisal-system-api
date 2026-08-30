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
                "propertyType matches appraisals having at least one property of the given type(s). "
                + "Single-column search: customerName, appraisalNumber and requestNumber each match one "
                + "column instead of the three `search` ORs together — appraisalNumber is the cheapest, "
                + "since it is the only one of the three on the base table. "
                + "subDistrict is an exact match on the 6-digit TIS-1099 geocode (not a name), and like "
                + "province/district it tests only the appraisal's FIRST land property. "
                + "LIKE metacharacters (% _ [ \\) are treated as literal text in every text filter.")
            .WithTags("Appraisal");
    }
}
