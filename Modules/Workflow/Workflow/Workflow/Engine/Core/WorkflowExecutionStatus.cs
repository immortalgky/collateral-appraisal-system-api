namespace Workflow.Workflow.Engine.Core;

public enum WorkflowExecutionStatus
{
    Running,
    Completed,
    StepCompleted,
    Failed,
    Pending
}