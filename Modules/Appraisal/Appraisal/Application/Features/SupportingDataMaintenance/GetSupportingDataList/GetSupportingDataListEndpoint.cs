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
            [FromQuery] DateTime? importDate,
            [FromQuery] string? supportingNumber,
            ISender sender,
            CancellationToken cancellationToken
        ) =>
        {
            var filter = new GetSupportingDataListQuery(pagination.PageNumber, pagination.PageSize, status, importDate, supportingNumber);

            var result = await sender.Send(filter, cancellationToken);

            return Results.Ok(result);
        })
        .WithName("GetSupportingDataList")
        .Produces<GetSupportingDataListResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get supporting data list")
        .WithDescription("Retrieves a paginated list of supporting data records with optional filtering by status, import data, and supporting number.");
    }
}