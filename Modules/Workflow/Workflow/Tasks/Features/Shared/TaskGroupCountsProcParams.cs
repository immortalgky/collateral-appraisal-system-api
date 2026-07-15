using Dapper;

namespace Workflow.Tasks.Features.Shared;

/// <summary>
/// Builds the <c>workflow.sp_GetTaskGroupCounts</c> parameter bag from an
/// <see cref="ITaskListFilter"/>. Mirrors <see cref="TaskListProcParams"/> but omits the
/// sort/paging parameters (this proc always returns the full grouped distribution) and adds
/// <c>@GroupBy</c>.
/// </summary>
public static class TaskGroupCountsProcParams
{
    public static DynamicParameters Build(
        string assignedType,
        IReadOnlyCollection<string> assignees,
        int companyGate,
        Guid? callerCompanyId,
        ITaskListFilter? filter,
        string groupBy)
    {
        static string? N(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;
        // Prefix-by-default LIKE pattern; user '*' = wildcard. See TaskListFilterBuilder.BuildSearchPattern.
        static string? Pattern(string? v) =>
            string.IsNullOrWhiteSpace(v) ? null : TaskListFilterBuilder.BuildSearchPattern(v);

        var p = new DynamicParameters();
        p.Add("AssignedType", assignedType);
        p.Add("Assignees", string.Join(",", assignees));
        p.Add("CompanyGate", companyGate);
        p.Add("CallerCompanyId", callerCompanyId);

        p.Add("Status", N(filter?.Status));
        p.Add("Priority", N(filter?.Priority));
        p.Add("TaskName", N(filter?.TaskName));
        p.Add("Search", Pattern(filter?.Search));
        p.Add("AppraisalNumber", Pattern(filter?.AppraisalNumber));
        p.Add("CustomerName", Pattern(filter?.CustomerName));
        p.Add("TaskStatus", N(filter?.TaskStatus));
        p.Add("TaskType", N(filter?.TaskType));
        p.Add("DateFrom", filter?.DateFrom);
        p.Add("DateTo", filter?.DateTo);
        p.Add("AppointmentDateFrom", filter?.AppointmentDateFrom);
        p.Add("AppointmentDateTo", filter?.AppointmentDateTo);
        p.Add("RequestedAtFrom", filter?.RequestedAtFrom);
        p.Add("RequestedAtTo", filter?.RequestedAtTo);
        p.Add("SlaStatus", N(filter?.SlaStatus));
        p.Add("ActivityId", N(filter?.ActivityId));
        p.Add("Purpose", N(filter?.Purpose));
        p.Add("TaskStatusBucket", N(filter?.TaskStatusBucket));

        p.Add("GroupBy", groupBy);
        return p;
    }
}
