using Shared.CQRS;
using Shared.Identity;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalViews;

/// <summary>
/// Builds the smart view presets list in memory.
/// No database access — filter values are either static strings or derived from the current user context.
/// </summary>
public class GetAppraisalViewsQueryHandler(
    ICurrentUserService currentUserService
) : IQueryHandler<GetAppraisalViewsQuery, GetAppraisalViewsResult>
{
    public Task<GetAppraisalViewsResult> Handle(
        GetAppraisalViewsQuery query,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var todayStr = today.ToString("yyyy-MM-dd");
        var plusThreeDays = today.AddDays(3).ToString("yyyy-MM-dd");

        var activeStatuses = "Pending,Assigned,InProgress,UnderReview";

        var views = new List<SmartViewDto>
        {
            BuildMyAssignments(currentUserService.Username),
            new(
                Key: "sla-at-risk",
                Name: "SLA At Risk",
                Description: "Active appraisals where the SLA is at risk or already breached",
                Filters: new Dictionary<string, string>
                {
                    ["slaStatus"] = "AtRisk,Breached",
                    ["status"] = activeStatuses
                }
            ),
            new(
                Key: "todays-appointments",
                Name: "Today's Appointments",
                Description: "Appraisals with a site visit scheduled for today",
                Filters: new Dictionary<string, string>
                {
                    ["appointmentDateFrom"] = todayStr,
                    ["appointmentDateTo"] = todayStr
                }
            ),
            new(
                Key: "unassigned",
                Name: "Unassigned",
                Description: "Appraisals that have not yet been assigned",
                Filters: new Dictionary<string, string>
                {
                    ["status"] = "Pending"
                }
            ),
            new(
                Key: "high-priority-active",
                Name: "High Priority Active",
                Description: "High priority appraisals that are currently in progress",
                Filters: new Dictionary<string, string>
                {
                    ["priority"] = "High",
                    ["status"] = activeStatuses
                }
            ),
            new(
                Key: "nearing-deadline",
                Name: "Nearing Deadline",
                Description: "Appraisals whose SLA due date falls within the next 3 days",
                Filters: new Dictionary<string, string>
                {
                    ["slaDueDateFrom"] = todayStr,
                    ["slaDueDateTo"] = plusThreeDays
                }
            ),
            new(
                Key: "external-assignments",
                Name: "External Assignments",
                Description: "Appraisals assigned to external companies that are active",
                Filters: new Dictionary<string, string>
                {
                    ["assignmentType"] = "External",
                    ["status"] = "Assigned,InProgress"
                }
            ),
            BuildMyCompanyQueue(currentUserService.CompanyId)
        };

        return Task.FromResult(new GetAppraisalViewsResult(views));
    }

    private static SmartViewDto BuildMyAssignments(string? username)
    {
        var filters = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(username))
            filters["assigneeUserId"] = username;

        return new SmartViewDto(
            Key: "my-assignments",
            Name: "My Assignments",
            Description: "Appraisals currently assigned to you",
            Filters: filters
        );
    }

    private static SmartViewDto BuildMyCompanyQueue(Guid? companyId)
    {
        var filters = new Dictionary<string, string>();

        if (companyId.HasValue)
            filters["assigneeCompanyId"] = companyId.Value.ToString();

        return new SmartViewDto(
            Key: "my-company-queue",
            Name: "My Company Queue",
            Description: "Appraisals assigned to your company",
            Filters: filters
        );
    }
}
