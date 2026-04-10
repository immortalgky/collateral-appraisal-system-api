using Carter;
using Dapper;
using Shared.Data;
using Shared.Pagination;

namespace Workflow.Sla.Features;

public class SlaEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sla").WithTags("SLA");

        group.MapGet("/overdue-tasks", GetOverdueTasks);
        group.MapGet("/compliance-summary", GetComplianceSummary);
        group.MapGet("/dashboard", GetDashboard);
    }

    private static async Task<IResult> GetOverdueTasks(
        ISqlConnectionFactory connectionFactory,
        int? pageNumber,
        int? pageSize,
        string? taskName,
        string? assignedTo,
        string? slaStatus,
        Guid? companyId)
    {
        var sql = """
            SELECT
                TaskId, CorrelationId, TaskName, AssignedTo, AssignedType,
                AssignedAt, DueAt, SlaStatus, SlaBreachedAt, WorkingBy,
                ElapsedHours, RemainingHours,
                AppraisalId, AppraisalNumber, AppraisalType,
                RequestId, RequestPurpose,
                WorkflowInstanceId, WorkflowDueAt, WorkflowSlaStatus,
                CompanyId
            FROM workflow.vw_SlaTaskList
            WHERE 1=1
            """;

        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(taskName))
        {
            sql += " AND TaskName = @TaskName";
            parameters.Add("TaskName", taskName);
        }

        if (!string.IsNullOrWhiteSpace(assignedTo))
        {
            sql += " AND AssignedTo = @AssignedTo";
            parameters.Add("AssignedTo", assignedTo);
        }

        if (!string.IsNullOrWhiteSpace(slaStatus))
        {
            sql += " AND SlaStatus = @SlaStatus";
            parameters.Add("SlaStatus", slaStatus);
        }

        if (companyId.HasValue)
        {
            sql += " AND CompanyId = @CompanyId";
            parameters.Add("CompanyId", companyId.Value);
        }

        var pagination = new PaginationRequest(pageNumber ?? 0, pageSize ?? 20);
        var result = await connectionFactory.QueryPaginatedAsync<SlaTaskDto>(
            sql, "DueAt ASC", pagination, parameters);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetComplianceSummary(
        ISqlConnectionFactory connectionFactory,
        string? taskName,
        Guid? companyId,
        string? assignedTo,
        int? year,
        int? month)
    {
        var sql = """
            SELECT
                TaskName, TotalTasks, OnTimeCount, AtRiskCount, BreachedCount, NoSlaCount,
                AvgCompletionHours, MinCompletionHours, MaxCompletionHours,
                CompanyId, CompletionMonth, CompletionYear, AssignedTo
            FROM workflow.vw_SlaComplianceSummary
            WHERE 1=1
            """;

        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(taskName))
        {
            sql += " AND TaskName = @TaskName";
            parameters.Add("TaskName", taskName);
        }

        if (companyId.HasValue)
        {
            sql += " AND CompanyId = @CompanyId";
            parameters.Add("CompanyId", companyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(assignedTo))
        {
            sql += " AND AssignedTo = @AssignedTo";
            parameters.Add("AssignedTo", assignedTo);
        }

        if (year.HasValue)
        {
            sql += " AND CompletionYear = @Year";
            parameters.Add("Year", year.Value);
        }

        if (month.HasValue)
        {
            sql += " AND CompletionMonth = @Month";
            parameters.Add("Month", month.Value);
        }

        using var connection = connectionFactory.GetOpenConnection();
        var results = await connection.QueryAsync<ComplianceSummaryDto>(sql, parameters);
        return Results.Ok(results);
    }

    private static async Task<IResult> GetDashboard(ISqlConnectionFactory connectionFactory)
    {
        using var connection = connectionFactory.GetOpenConnection();

        var taskCounts = await connection.QueryFirstOrDefaultAsync<DashboardTaskCounts>("""
            SELECT
                COUNT(*) AS TotalActiveTasks,
                SUM(CASE WHEN SlaStatus = 'ON_TIME' THEN 1 ELSE 0 END) AS OnTimeCount,
                SUM(CASE WHEN SlaStatus = 'AT_RISK' THEN 1 ELSE 0 END) AS AtRiskCount,
                SUM(CASE WHEN SlaStatus = 'BREACHED' THEN 1 ELSE 0 END) AS BreachedCount
            FROM workflow.vw_SlaTaskList
            """);

        var workflowCounts = await connection.QueryFirstOrDefaultAsync<DashboardWorkflowCounts>("""
            SELECT
                COUNT(*) AS TotalRunningWorkflows,
                SUM(CASE WHEN WorkflowSlaStatus = 'ON_TIME' THEN 1 ELSE 0 END) AS OnTimeCount,
                SUM(CASE WHEN WorkflowSlaStatus = 'AT_RISK' THEN 1 ELSE 0 END) AS AtRiskCount,
                SUM(CASE WHEN WorkflowSlaStatus = 'BREACHED' THEN 1 ELSE 0 END) AS BreachedCount
            FROM workflow.vw_WorkflowSlaSummary
            WHERE Status = 'RUNNING'
            """);

        return Results.Ok(new DashboardResult(taskCounts, workflowCounts));
    }
}

// DTOs
public record SlaTaskDto(
    Guid TaskId, Guid CorrelationId, string TaskName, string AssignedTo, string AssignedType,
    DateTime AssignedAt, DateTime? DueAt, string? SlaStatus, DateTime? SlaBreachedAt, string? WorkingBy,
    int? ElapsedHours, int? RemainingHours,
    Guid? AppraisalId, string? AppraisalNumber, string? AppraisalType,
    Guid? RequestId, string? RequestPurpose,
    Guid? WorkflowInstanceId, DateTime? WorkflowDueAt, string? WorkflowSlaStatus,
    Guid? CompanyId);

public record ComplianceSummaryDto(
    string TaskName, int TotalTasks, int OnTimeCount, int AtRiskCount, int BreachedCount, int NoSlaCount,
    int? AvgCompletionHours, int? MinCompletionHours, int? MaxCompletionHours,
    Guid? CompanyId, int CompletionMonth, int CompletionYear, string AssignedTo);

public record DashboardTaskCounts(
    int TotalActiveTasks, int OnTimeCount, int AtRiskCount, int BreachedCount);

public record DashboardWorkflowCounts(
    int TotalRunningWorkflows, int OnTimeCount, int AtRiskCount, int BreachedCount);

public record DashboardResult(DashboardTaskCounts? Tasks, DashboardWorkflowCounts? Workflows);
