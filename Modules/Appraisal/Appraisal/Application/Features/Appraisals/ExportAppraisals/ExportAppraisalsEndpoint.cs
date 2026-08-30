using Appraisal.Application.Features.Appraisals.GetAppraisals;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.Appraisals.ExportAppraisals;

public class ExportAppraisalsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/export",
                async (
                    [AsParameters] AppraisalListQueryParams queryParams,
                    // Export format: "xlsx" (default) or "csv"
                    [FromQuery] string? format,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var filter = queryParams.ToFilterRequest();

                    var query = new ExportAppraisalsQuery(filter, format ?? "xlsx");
                    var result = await sender.Send(query, cancellationToken);

                    return Results.File(result.FileBytes, result.ContentType, result.FileName);
                }
            )
            .WithName("ExportAppraisals")
            .Produces(StatusCodes.Status200OK)
            .WithSummary("Export appraisals to file")
            .WithDescription(
                "Exports all matching appraisals (up to 10,000 rows) as a file download. " +
                "Accepts the same filter parameters as GET /appraisals. " +
                "Use format=xlsx (default) for Excel or format=csv for CSV with UTF-8 BOM.")
            .WithTags("Appraisal");
    }
}
