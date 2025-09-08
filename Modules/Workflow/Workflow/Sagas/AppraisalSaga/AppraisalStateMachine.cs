using Shared.Messaging.Events;
using Shared.Messaging.Values;

namespace Workflow.Sagas.AppraisalSaga;

public partial class AppraisalStateMachine : MassTransitStateMachine<AppraisalSagaState>
{
    // Service
    private readonly IDateTimeProvider _dateTimeProvider;

    // States representing workflow activities
    public State RequestMakerAwaitingAssignment { get; private set; } = default!;
    public State AdminAwaitingAssignment { get; private set; } = default!;
    public State AppraisalStaffAwaitingAssignment { get; private set; } = default!;
    public State AppraisalCheckerAwaitingAssignment { get; private set; } = default!;
    public State AppraisalVerifierAwaitingAssignment { get; private set; } = default!;
    public State AppraisalCommitteeAwaitingAssignment { get; private set; } = default!;
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

    public Dictionary<State, TaskName> StateTaskNames { get; private set; } = default!;
    public AppraisalStateMachine(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
        StateTaskNames = new()
        {
            [RequestMakerAwaitingAssignment] = TaskName.RequestMaker,
            [RequestMaker] = TaskName.RequestMaker,
            [AdminAwaitingAssignment] = TaskName.Admin,
            [Admin] = TaskName.Admin,
            [AppraisalStaffAwaitingAssignment] = TaskName.AppraisalStaff,
            [AppraisalStaff] = TaskName.AppraisalStaff,
            [AppraisalCheckerAwaitingAssignment] = TaskName.AppraisalChecker,
            [AppraisalChecker] = TaskName.AppraisalChecker,
            [AppraisalVerifierAwaitingAssignment] = TaskName.AppraisalVerifier,
            [AppraisalVerifier] = TaskName.AppraisalVerifier,
        };

        InstanceState(x => x.CurrentState);

        Event(() => RequestSubmitted, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => TaskCompleted, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => TaskAssigned, x => x.CorrelateById(m => m.Message.CorrelationId));

        Initially(
            When(RequestSubmitted)
                .Then(Initialize)
                .TransitionTo(AdminAwaitingAssignment)
                .Then(context => AssignmentRequest(context, TaskName.Admin))
        );

        DuringAwaitingAssignment(AdminAwaitingAssignment, Admin);
        DuringProcess(Admin, AppraisalStaffAwaitingAssignment, RequestMakerAwaitingAssignment);

        DuringAwaitingAssignment(RequestMakerAwaitingAssignment, RequestMaker);
        DuringProcess(RequestMaker, AdminAwaitingAssignment);

        DuringAwaitingAssignment(AppraisalStaffAwaitingAssignment, AppraisalStaff);
        DuringProcess(AppraisalStaff, AppraisalCheckerAwaitingAssignment, AdminAwaitingAssignment);

        DuringAwaitingAssignment(AppraisalCheckerAwaitingAssignment, AppraisalChecker);
        DuringProcess(AppraisalChecker, AppraisalVerifierAwaitingAssignment, AppraisalStaffAwaitingAssignment);

        DuringAwaitingAssignment(AppraisalVerifierAwaitingAssignment, AppraisalVerifier);
        DuringProcess(AppraisalVerifier, routeBackState: AppraisalCheckerAwaitingAssignment);

        SetCompletedWhenFinalized();
    }
    private void DuringAwaitingAssignment(State currState, State nextState)
    {
        During(currState,
            When(TaskAssigned)
                .If(context => context.Message.TaskName == StateTaskNames[currState], then => then
                    .TransitionTo(nextState)
                    .Then(TransitionCompleted))
        );
    }

    private void DuringProcess(State currState, State? nextState = null, State? routeBackState = null)
    {
        During(currState,
            When(TaskCompleted)
                .If(context => context.Message.TaskName == StateTaskNames[currState], then => then
                    .Then(CompleteActivity)
                    .If(context => context.Message.ActionTaken == "P", proceed =>
                    {
                        if (nextState != null)
                        {
                            return proceed
                            .TransitionTo(nextState)
                            .Then(context => AssignmentRequest(context, StateTaskNames[nextState]));
                        }
                        else
                        {
                            return proceed.Finalize();
                        }
                    })
                    .If(context => context.Message.ActionTaken == "R", routeBack =>
                    {
                        if (routeBackState != null)
                        {
                            return routeBack
                                .TransitionTo(routeBackState)
                                .Then(context => AssignmentRequest(context, StateTaskNames[routeBackState]));
                        }
                        else
                        {
                            return routeBack;
                        }
                    })
                )
        );
    }
}