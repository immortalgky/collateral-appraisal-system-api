using Carter;
using Common.Application.Features.Monitoring.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Pagination;

namespace Common.Application.Features.Monitoring.GetMeetingFollowups;

public class GetMeetingFollowupsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/monitoring/meeting-followups",
                async (
                    [AsParameters] PaginationRequest pagination,
                    string? search,
                    string? sortBy,
                    string? sortDir,
                    int[]? tier,
                    string[]? slaStatus,
                    string[]? slaBucket,
                    string? meetingNumber,
                    DateOnly? meetingDateFrom,
                    DateOnly? meetingDateTo,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var filter = new MeetingFollowupFilter(
                        search, sortBy, sortDir, tier, slaStatus, slaBucket,
                        meetingNumber, meetingDateFrom, meetingDateTo);
                    var result = await sender.Send(new GetMeetingFollowupsQuery(pagination, filter), cancellationToken);
                    return Results.Ok(result);
                })
            .WithName("MonitoringGetMeetingFollowups")
            .Produces<PaginatedResult<MeetingFollowupDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Monitoring: Pending Approval Followup list")
            .WithDescription("Returns pending committee-approval tasks (one row per appraisal, all 3 tiers). Requires any MONITORING:MEETING_FOLLOWUP* permission.")
            .WithTags("Monitoring")
            .RequireAuthorization(MonitoringPermissions.PolicyMeetingFollowup);
    }
}
