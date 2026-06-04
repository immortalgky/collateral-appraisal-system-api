namespace Auth.Application.Features.Reports.ExportUserAccessMatrix;

public class ExportUserAccessMatrixEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/reports/user-access/export",
                async (
                    string? scope = null,
                    Guid? companyId = null,
                    string? roleName = null,
                    Guid? groupId = null,
                    Guid? teamId = null,
                    bool? isActive = null,
                    string? search = null,
                    ISender sender = default!,
                    CancellationToken cancellationToken = default) =>
                {
                    var query = new ExportUserAccessMatrixQuery(
                        scope, companyId, roleName, groupId, teamId, isActive, search);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.File(result.Bytes, "text/csv", result.FileName);
                })
            .WithName("ExportUserAccessMatrix")
            .Produces<byte[]>(contentType: "text/csv")
            .WithSummary("Export user access matrix as CSV")
            .WithDescription("Streams all matching users (no pagination) with their roles, groups, and teams as a downloadable CSV file.")
            .WithTags("AuthReports")
            .RequireAuthorization("CanViewAuthAudit");
    }
}
