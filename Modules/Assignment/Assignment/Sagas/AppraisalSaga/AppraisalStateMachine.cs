using Shared.Messaging.Events;

namespace Assignment.Sagas.AppraisalSaga;

public partial class AppraisalStateMachine : MassTransitStateMachine<AppraisalSagaState>
{
    // Service
    private readonly IDateTimeProvider _dateTimeProvider;

    // States representing workflow activities
    public State AwaitingAssignment { get; private set; } = default!;
    public State RequestMaker { get; private set; } = default!;
    public State RequestChecker { get; private set; } = default!;
    public State Admin { get; private set; } = default!;
    public State AppraisalStaff { get; private set; } = default!;
    public State AppraisalChecker { get; private set; } = default!;
    public State AppraisalVerifier { get; private set; } = default!;
    public State AppraisalCommittee { get; private set; } = default!;

    // Events for workflow activities
    public Event<RequestSubmitted> RequestSubmitted { get; private set; } = default!;
    public Event<TaskCompleted> TaskCompleted { get; private set; } = default!;
    public Event<TaskAssigned> TaskAssigned { get; private set; } = default!;

    public AppraisalStateMachine(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;

        InstanceState(x => x.CurrentState);

        Event(() => RequestSubmitted, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => TaskCompleted, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => TaskAssigned, x => x.CorrelateById(m => m.Message.CorrelationId));

        Initially(
            When(RequestSubmitted)
                .Then(Initialize)
                .TransitionTo(Admin)
                .Then(AssignmentRequest)
        );

        // Admin
        During(Admin,
            When(TaskAssigned)
                .If(context => context.Message.TaskName == "Admin", then => then
                    .Then(TransitionCompleted)
                ),
            When(TaskCompleted)
                .If(context => context.Message.TaskName == "Admin", then => then
                    .Then(CompleteActivity)
                    .If(context => context.Message.ActionTaken == "P", proceed => proceed
                        .TransitionTo(AppraisalStaff)
                        .Then(AssignmentRequest)
                    )
                    .If(context => context.Message.ActionTaken == "R", routeBack => routeBack
                        .TransitionTo(RequestMaker)
                        .Then(AssignmentRequest)
                    )
                )
        );

        // Appraisal Staff
        During(AppraisalStaff,
            When(TaskAssigned)
                .If(context => context.Message.TaskName == "AppraisalStaff", then => then
                    .Then(TransitionCompleted)
                ),
            When(TaskCompleted)
                .If(context => context.Message.TaskName == "AppraisalStaff", then => then
                    .Then(CompleteActivity)
                    .If(context => context.Message.ActionTaken == "P", proceed => proceed
                        .TransitionTo(AppraisalChecker)
                        .Then(AssignmentRequest)
                    )
                    .If(context => context.Message.ActionTaken == "R", routeBack => routeBack
                        .TransitionTo(Admin)
                        .Then(AssignmentRequest)
                    )
                )
        );

        // Appraisal Checker
        During(AppraisalChecker,
            When(TaskAssigned)
                .If(context => context.Message.TaskName == "AppraisalChecker", then => then
                    .Then(TransitionCompleted)
                ),
            When(TaskCompleted)
                .If(context => context.Message.TaskName == "AppraisalChecker", then => then
                    .Then(CompleteActivity)
                    .If(context => context.Message.ActionTaken == "P", proceed => proceed
                        .TransitionTo(AppraisalVerifier)
                        .Then(AssignmentRequest)
                    )
                    .If(context => context.Message.ActionTaken == "R", routeBack => routeBack
                        .TransitionTo(AppraisalStaff)
                        .Then(AssignmentRequest)
                    )
                )
        );

        // Appraisal Verifier
        During(AppraisalVerifier,
            When(TaskAssigned)
                .If(context => context.Message.TaskName == "AppraisalVerifier", then => then
                    .Then(TransitionCompleted)
                ),
            When(TaskCompleted)
                .If(context => context.Message.TaskName == "AppraisalVerifier", then => then
                    .Then(CompleteActivity)
                    .If(context => context.Message.ActionTaken == "P", proceed => proceed
                        .Finalize()
                    )
                    .If(context => context.Message.ActionTaken == "R", routeBack => routeBack
                        .TransitionTo(AppraisalChecker)
                        .Then(AssignmentRequest)
                    )
                )
        );

        SetCompletedWhenFinalized();
    }
}