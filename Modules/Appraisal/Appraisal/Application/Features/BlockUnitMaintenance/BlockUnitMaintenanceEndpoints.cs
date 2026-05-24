using Appraisal.Application.Features.BlockUnitMaintenance.GetBlockUnitMaintenanceList;
using Appraisal.Application.Features.BlockUnitMaintenance.GetBlockUnitMaintenanceUnits;
using Appraisal.Application.Features.BlockUnitMaintenance.UpdateProjectUnitSaleInfo;
using Appraisal.Domain.Projects;

namespace Appraisal.Application.Features.BlockUnitMaintenance;

public class BlockUnitMaintenanceEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // GET /block-unit-maintenance — paginated project list with aggregate unit counts.
        app.MapGet("/block-unit-maintenance", async (
                ISender sender,
                [FromQuery] int pageNumber = 0,
                [FromQuery] int pageSize = 20,
                [FromQuery] string? search = null,
                [FromQuery] string? projectType = null,
                [FromQuery] string? developer = null,
                [FromQuery] string? sortBy = null,
                [FromQuery] string? sortDir = null,
                CancellationToken ct = default) =>
            {
                var query = new GetBlockUnitMaintenanceListQuery(
                    pageNumber, pageSize, search, projectType, developer, sortBy, sortDir);
                var result = await sender.Send(query, ct);
                return Results.Ok(result);
            })
            .WithName("GetBlockUnitMaintenanceList")
            .Produces<BlockUnitMaintenanceListResult>()
            .WithSummary("List all projects for block unit maintenance")
            .WithDescription(
                "Returns a paginated list of projects with aggregated unit counts for admin maintenance.")
            .WithTags("BlockUnitMaintenance")
            .RequireAuthorization();

        // GET /block-unit-maintenance/{projectId}/units — unit rows for a project.
        app.MapGet("/block-unit-maintenance/{projectId:guid}/units", async (
                Guid projectId,
                ISender sender,
                CancellationToken ct = default) =>
            {
                var query = new GetBlockUnitMaintenanceUnitsQuery(projectId);
                var result = await sender.Send(query, ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithName("GetBlockUnitMaintenanceUnits")
            .Produces<BlockUnitMaintenanceDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get units for a project")
            .WithDescription(
                "Returns all units for the given project with their current sale-tracking fields.")
            .WithTags("BlockUnitMaintenance")
            .RequireAuthorization();

        // PUT /block-unit-maintenance/{projectId}/units — bulk update sale info.
        app.MapPut("/block-unit-maintenance/{projectId:guid}/units", async (
                Guid projectId,
                UpdateProjectUnitSaleInfoRequest request,
                ISender sender,
                CancellationToken ct = default) =>
            {
                var items = request.Items
                    .Select(i => new UnitSaleInfoItem(
                        i.UnitId,
                        i.IsSold,
                        i.PurchaseBy,
                        i.LoanBankName))
                    .ToList()
                    .AsReadOnly();

                var command = new UpdateProjectUnitSaleInfoCommand(projectId, items);
                await sender.Send(command, ct);
                return Results.NoContent();
            })
            .WithName("UpdateProjectUnitSaleInfo")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Bulk-update unit sale info")
            .WithDescription(
                "Updates IsSold, PurchaseBy, and LoanBankName for one or more units within the project.")
            .WithTags("BlockUnitMaintenance")
            .RequireAuthorization();
    }
}
