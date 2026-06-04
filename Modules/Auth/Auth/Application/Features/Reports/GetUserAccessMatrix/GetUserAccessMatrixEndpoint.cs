namespace Auth.Application.Features.Reports.GetUserAccessMatrix;

public class GetUserAccessMatrixEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/reports/user-access",
                async (
                    string? scope = null,
                    Guid? companyId = null,
                    string? roleName = null,
                    Guid? groupId = null,
                    Guid? teamId = null,
                    bool? isActive = null,
                    string? search = null,
                    int pageNumber = 0,
                    int pageSize = 20,
                    ISender sender = default!,
                    CancellationToken cancellationToken = default) =>
                {
                    var query = new GetUserAccessMatrixQuery(
                        scope, companyId, roleName, groupId, teamId,
                        isActive, search, pageNumber, pageSize);

                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetUserAccessMatrix")
            .Produces<GetUserAccessMatrixResult>()
            .WithSummary("Get user access matrix report")
            .WithDescription("Paginated, filterable report of users with their roles, groups, and teams (0-based page).")
            .WithTags("AuthReports")
            .RequireAuthorization("CanViewAuthAudit");
    }
}
