using Assignment.Workflow.Activities.Core;
using Assignment.Workflow.Schema;

namespace Assignment.Workflow.Activities.Factories;

public interface IWorkflowActivityFactory
{
    IWorkflowActivity CreateActivity(string activityType);
    IEnumerable<string> GetAvailableActivityTypes();
    ActivityTypeDefinition GetActivityTypeDefinition(string activityType);
}