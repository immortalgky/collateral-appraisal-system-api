using Carter;
using Integration.FileInterface.Format.Reappraisal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.FileInterface.Jobs.Reappraisal;

/// <summary>
/// Generates an AS400 COLLATREV file from real completed appraisals and drops it into the ingestion
/// inbox (directory from FileInterfaceConfigs), so the reappraisal-as400 job can consume it end-to-end.
/// Lets QA produce test files while the real AS400 feed isn't available.
/// Restricted to the reappraisal.generate-test-file permission (Admin only).
///
/// With <c>autoUpdate=Y</c> the file's collateral ids are synthetic (60000000 + n), so a test run
/// against an AS400 that applies 'Y' could overwrite a real collateral with a colliding id. Use it
/// only where the AS400 side is a test system.
/// </summary>
public class GenerateTestFileEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/reappraisal/generate-test-file", async (
                CollatrevTestFileBuilder builder,
                int? count,
                string? date,
                string? autoUpdate,
                CancellationToken cancellationToken) =>
            {
                var rowCount = Math.Clamp(count ?? 20, 1, 500);

                DateOnly fileDate;
                if (string.IsNullOrWhiteSpace(date))
                    fileDate = DateOnly.FromDateTime(DateTime.Now);
                else if (!DateOnly.TryParseExact(date, "yyyyMMdd", out fileDate))
                    return Results.BadRequest("date must be in yyyyMMdd format");

                // The only validation: an invalid value is a 400, and the builder takes the normalised flag as given.
                if (!CollatrevAutoUpdate.TryNormalize(autoUpdate, out var flag))
                    return Results.BadRequest("autoUpdate must be Y or N");

                var result = await builder.GenerateAsync(rowCount, fileDate, flag, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GenerateReappraisalTestFile")
            .WithSummary("Generate a COLLATREV reappraisal file from real completed appraisals")
            .WithDescription(
                "Queries completed appraisals and writes a fixed-width AS400 COLLATREV file into the " +
                "ingestion inbox. Query params: count (default 20, clamped 1-500), " +
                "date (yyyyMMdd, default today), " +
                "autoUpdate (Y or N, written to position 650 of every row; default blank).")
            .RequireAuthorization("reappraisal.generate-test-file")
            .Produces<GenerateReappraisalTestFileResult>()
            .WithTags("Reappraisal");
    }
}
