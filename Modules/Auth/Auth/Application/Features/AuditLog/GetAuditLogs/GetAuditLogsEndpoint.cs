using Auth.Domain.Auditing;

namespace Auth.Application.Features.AuditLog.GetAuditLogs;

public class GetAuditLogsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/auth/audit-logs",
                async (
                    int pageNumber = 0,
                    int pageSize = 20,
                    AuditEntityType? entityType = null,
                    Guid? entityId = null,
                    Guid? actorUserId = null,
                    DateTime? from = null,
                    DateTime? to = null,
                    AuditAction? action = null,
                    ISender sender = default!,
                    CancellationToken cancellationToken = default) =>
                {
                    var query = new GetAuditLogsQuery(
                        pageNumber, pageSize,
                        entityType, entityId, actorUserId,
                        from, to, action);
                    var result = await sender.Send(query, cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("GetAuditLogs")
            .Produces<GetAuditLogsResult>()
            .WithSummary("Get auth audit logs")
            .WithDescription("Paginated, filterable list of auth module audit events (newest first).")
            .WithTags("AuthAudit")
            .RequireAuthorization("CanViewAuthAudit");
    }
}
