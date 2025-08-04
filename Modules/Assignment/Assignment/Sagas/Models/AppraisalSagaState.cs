namespace Assignment.Sagas.Models;

/// <summary>
/// Represents the state of an appraisal saga within the application's distributed workflow.
/// It contains properties essential for tracking the progress, assignments, and updates in the saga lifecycle.
/// </summary>
public class AppraisalSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public long RequestId { get; set; }
    public string CurrentState { get; set; } = default!;

    public string Assignee { get; set; } = default!;
    public string AssignType { get; set; } = default!;

    public DateTime StartedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}