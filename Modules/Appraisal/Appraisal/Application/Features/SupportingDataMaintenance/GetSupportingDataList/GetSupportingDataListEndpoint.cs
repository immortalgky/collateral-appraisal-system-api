using Microsoft.AspNetCore.Mvc;
using Shared.Pagination;

namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataList;

public class GetSupportingDataListEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/supporting-data", async (
            [AsParameters] PaginationRequest pagination,
            [FromQuery] string? status,
            [FromQuery] DateTime? dateFrom,
            [FromQuery] DateTime? dateTo,
            [FromQuery] DateTime? lastModifiedDateFrom,
            [FromQuery] DateTime? lastModifiedDateTo,
            [FromQuery] string? supportingNumber,
            [FromQuery] string? search,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDir,
            ISender sender,
            CancellationToken cancellationToken
        ) =>
        {
            var filter = new GetSupportingDataListQuery(pagination.PageNumber, pagination.PageSize, status, dateFrom, dateTo, lastModifiedDateFrom, lastModifiedDateTo, supportingNumber, search, sortBy, sortDir);

            var result = await sender.Send(filter, cancellationToken);

            return Results.Ok(result);
        })
        .WithName("GetSupportingDataList")
        .Produces<GetSupportingDataListResult>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get supporting data list")
        .WithDescription("Retrieves a paginated list of supporting data records with optional filtering by status, import data, and supporting number.")
        .RequireAuthorization();
    }
}