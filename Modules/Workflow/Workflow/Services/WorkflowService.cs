using Elsa.Workflows.Helpers;
using Elsa.Workflows.Management;
using Elsa.Workflows.Management.Filters;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Filters;
using Elsa.Workflows.Runtime.Messages;
using Elsa.Workflows.Runtime.Options;
using Workflow.Workflow.Activities;

namespace Workflow.Services;

public class WorkflowService(
    IEventPublisher eventPublisher,
    IBookmarkQueue bookmarkQueue,
    IStimulusHasher hasher
) : IWorkflowService
{
    public async System.Threading.Tasks.Task StartWorkflowAsync(Guid requestId)
    {
        await eventPublisher.PublishAsync("KickstartWorkflow",
            payload: new { RequestId = requestId, AssignTo = "TestUser", AssignType = "U" },
            correlationId: requestId.ToString());
    }

    public async System.Threading.Tasks.Task DecisionAsync(string correlationId, string activityName, string actiontaken)
    {
        await bookmarkQueue.EnqueueAsync(new NewBookmarkQueueItem
        {
            StimulusHash = hasher.Hash(ActivityTypeNameHelper.GenerateTypeName<UserTask>(), new UserTaskBookmark
            {
                TaskName = activityName,
            }),
            ActivityTypeName = ActivityTypeNameHelper.GenerateTypeName<UserTask>(),
            CorrelationId = correlationId,
            Options = new ResumeBookmarkOptions
            {
                Input = new Dictionary<string, object>
                {
                    { "ActionTaken", actiontaken }
                }
            }
        });
    }
}