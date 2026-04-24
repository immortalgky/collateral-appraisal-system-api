namespace Workflow.Tasks.Features.ExpireOverdueFanOutTasks;

/// <summary>
/// Archives all PendingTasks that belong to a specific fan-out activity and whose DueAt has passed.
/// After archiving, resumes the workflow so FanOutTaskActivity re-evaluates its all-terminal gate.
/// </summary>
public record ExpireOverdueFanOutTasksCommand(
    Guid WorkflowInstanceId,
    string ActivityId = "ext-collect-submissions"
) : ICommand<ExpireOverdueFanOutTasksResult>;

/// <param name="ArchivedCount">Number of PendingTasks archived as "Expired".</param>
/// <param name="ExpiredCompanyIds">AssigneeCompanyId of each archived task (used by caller to decline CompanyQuotations).</param>
public record ExpireOverdueFanOutTasksResult(
    int ArchivedCount,
    IReadOnlyList<Guid> ExpiredCompanyIds
);
