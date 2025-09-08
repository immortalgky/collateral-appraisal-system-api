using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Schema;

namespace Workflow.Workflow.Activities.Factories;

public interface IWorkflowActivityFactory
{
    IWorkflowActivity CreateActivity(string activityType);
    IEnumerable<string> GetAvailableActivityTypes();
    ActivityTypeDefinition GetActivityTypeDefinition(string activityType);
}