using Shared.Messaging.Events;

namespace Assignment.Sagas.AppraisalSaga;

/// <summary>
/// Represents the state machine responsible for managing the lifecycle of an appraisal saga.
/// The <c>AppraisalStateMachine</c> utilizes MassTransit's state machine framework to define
/// states, events, and transitions for the appraisal process.
/// </summary>
/// <remarks>
/// This class inherits from <c>MassTransitStateMachine&lt;AppraisalSagaState&gt;</c>,
/// integrating saga state persistence and behavior into the event-driven architecture.
/// </remarks>
/// <threadsafety>
/// Instances of this class are typically thread-safe, as state management and transitions are
/// handled through the MassTransit framework, which ensures safe concurrent access.
/// </threadsafety>
/// <seealso cref="MassTransitStateMachine{TState}"/>
/// <seealso cref="AppraisalSagaState"/>
public partial class AppraisalStateMachine
{
    /// <summary>
    /// Initializes the AppraisalSagaState with the provided context.
    /// </summary>
    /// <param name="context">The behavior context containing state and message details for the saga.</param>
    private void Initialize(BehaviorContext<AppraisalSagaState, RequestSubmitted> context)
    {
        context.Saga.RequestId = context.Message.RequestId;
        context.Saga.StartedAt = _dateTimeProvider.Now;
    }

    /// <summary>
    /// Handles an assignment request in the appraisal saga state machine.
    /// </summary>
    /// <typeparam name="T">The type of the message associated with the behavior context.</typeparam>
    /// <param name="context">The behavior context containing the saga state and message details.</param>
    private void AssignmentRequest<T>(BehaviorContext<AppraisalSagaState, T> context) where T : class
    {
        context.Saga.Assignee = "*System";
        context.Saga.AssignType = "S";

        context.Publish(new AssignmentRequested
            {
                CorrelationId = context.Saga.CorrelationId,
                TaskName = context.Saga.CurrentState,
            }
            , context.CancellationToken);
    }

    /// <summary>
    /// Completes an activity based on the provided context.
    /// </summary>
    /// <param name="context">The behavior context containing state and event details for task completion.</param>
    private void CompleteActivity(BehaviorContext<AppraisalSagaState, TaskCompleted> context)
    {
        context.Saga.LastUpdatedAt = _dateTimeProvider.Now;
    }

    /// <summary>
    /// Handles the transition completion event and updates the saga state
    /// with the provided context and assigned task details.
    /// </summary>
    /// <param name="context">The behavior context containing saga state, message details, and cancellation token associated with the process.</param>
    private void TransitionCompleted(BehaviorContext<AppraisalSagaState, TaskAssigned> context)
    {
        context.Saga.Assignee = context.Message.AssignedTo;
        context.Saga.AssignType = context.Message.AssignedType;

        context.Publish(new TransitionCompleted
            {
                CorrelationId = context.Saga.CorrelationId,
                RequestId = context.Saga.RequestId,
                TaskName = context.Saga.CurrentState,
                CurrentState = context.Saga.CurrentState,
                AssignedTo = context.Saga.Assignee,
                AssignedType = context.Saga.AssignType
            },
            context.CancellationToken);
    }
}