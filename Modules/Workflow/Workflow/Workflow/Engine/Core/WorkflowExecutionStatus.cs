namespace Workflow.Workflow.Engine.Core;

public enum WorkflowExecutionStatus
{
    Running,
    Completed,
    Failed,
    Pending,
    StepCompleted // New status for single-step execution completion
}