using Request.Infrastructure.Reappraisal;

namespace Request.Application.Features.Reappraisal.GenerateTestFile;

/// <summary>
/// Generates an AS400 COLLATREV file from real completed appraisals and drops it into the ingestion inbox,
/// so the existing reappraisal-as400 job can consume it end-to-end. Lets QA produce test files while the
/// real AS400 feed isn't available. Restricted to the REAPPRAISAL_GENERATE_TEST_FILE permission (Admin only).
/// </summary>
public class GenerateTestFileEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/reappraisal/generate-test-file", async (
                CollatrevTestFileBuilder builder,
                int? count,
                string? date,
                CancellationToken cancellationToken) =>
            {
                var rowCount = Math.Clamp(count ?? 20, 1, 500);

                DateOnly fileDate;
                if (string.IsNullOrWhiteSpace(date))
                    fileDate = DateOnly.FromDateTime(DateTime.UtcNow);
                else if (!DateOnly.TryParseExact(date, "yyyyMMdd", out fileDate))
                    return Results.BadRequest("date must be in yyyyMMdd format");

                var result = await builder.GenerateAsync(rowCount, fileDate, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GenerateReappraisalTestFile")
            .WithSummary("Generate a COLLATREV reappraisal file from real completed appraisals")
            .WithDescription(
                "Queries completed appraisals and writes a fixed-width AS400 COLLATREV file into the " +
                "ingestion inbox (Reappraisal:Local:Path). Query params: count (default 20, clamped 1-500), " +
                "date (yyyyMMdd, default today).")
            .RequireAuthorization("reappraisal.generate-test-file")
            .Produces<GenerateReappraisalTestFileResult>()
            .WithTags("Reappraisal");
    }
}
