using Carter;
using Integration.Infrastructure;
using Integration.Infrastructure.FileInterface;
using Microsoft.EntityFrameworkCore;
using Shared.Identity;

namespace Api.Endpoints.FileInterfaces;

/// <summary>
/// Read-only view over <c>integration.InboundFileLogs</c> — what arrived from AS400 and what happened
/// to it.
///
/// <b>Why it is needed.</b> Nothing blocks on these feeds any more: the ingest jobs run daily against
/// monthly files, and the exports run whether or not a new file has landed. That is the right
/// behaviour — holding completed appraisals back for weeks because a monthly file is late would be
/// worse than sending them with the collateral ids already on hand — but it means a file that never
/// arrives produces no failure anywhere. This endpoint is where that shows up.
/// </summary>
public class GetInboundFileLogEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/file-interfaces/admin/inbound-files",
                async (
                    IntegrationDbContext db,
                    ICurrentUserService currentUser,
                    string? interfaceCode,
                    int? limit,
                    CancellationToken cancellationToken) =>
                {
                    if (!currentUser.IsInRole("Admin") && !currentUser.IsInRole("IntAdmin"))
                        throw new UnauthorizedAccessException(
                            "Only Admin users can read the inbound file log.");

                    var take = Math.Clamp(limit ?? 50, 1, 500);

                    var query = db.InboundFileLogs.AsNoTracking();

                    if (!string.IsNullOrWhiteSpace(interfaceCode))
                        query = query.Where(l => l.InterfaceCode == interfaceCode);

                    // Newest attempt first. StartedAt rather than CompletedAt so a run that died
                    // mid-file still surfaces at the top instead of sinking below older successes.
                    var rows = await query
                        .OrderByDescending(l => l.StartedAt)
                        .Take(take)
                        .Select(l => new InboundFileLogRow(
                            l.InterfaceCode,
                            l.FileName,
                            l.FileDate,
                            l.SizeBytes,
                            l.Status.ToString(),
                            l.RowsReceived,
                            l.RowsUpdated,
                            l.RowsUnchanged,
                            l.StartedAt,
                            l.CompletedAt,
                            l.ErrorMessage))
                        .ToListAsync(cancellationToken);

                    return Results.Ok(rows);
                })
            .WithName("GetInboundFileLog")
            .Produces<List<InboundFileLogRow>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Inbound AS400 file history (admin)")
            .WithDescription(
                "Most recent inbound interface files and their outcome. Filter with ?interfaceCode= "
                + "(HOST_COLLATERAL_LINK, REAPPRAISAL) and ?limit=.")
            .WithTags("FileInterface")
            .RequireAuthorization();
    }
}

/// <param name="FileDate">
/// Parsed out of the file name. Used for ordering only — AS400 builds these files around midnight,
/// so the same batch can be stamped with either side of it. To answer "did this month's file arrive",
/// read <paramref name="CompletedAt"/>, which is our own clock.
/// </param>
public record InboundFileLogRow(
    string InterfaceCode,
    string FileName,
    DateOnly? FileDate,
    long SizeBytes,
    string Status,
    int RowsReceived,
    int RowsUpdated,
    int RowsUnchanged,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage);
