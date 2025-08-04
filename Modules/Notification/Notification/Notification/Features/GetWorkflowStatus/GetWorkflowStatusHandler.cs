using Notification.Notification.Dtos;
using Shared.Contracts.CQRS;

namespace Notification.Notification.Features.GetWorkflowStatus;

// public class GetWorkflowStatusHandler : IQueryHandler<GetWorkflowStatusQuery, GetWorkflowStatusResponse>
// {
//     private readonly AppraisalSagaDbContext _sagaDbContext;
//
//     public GetWorkflowStatusHandler(AppraisalSagaDbContext sagaDbContext)
//     {
//         _sagaDbContext = sagaDbContext;
//     }
//
//     public async Task<GetWorkflowStatusResponse> Handle(GetWorkflowStatusQuery request, CancellationToken cancellationToken)
//     {
//         var sagaState = await _sagaDbContext.Set<Assignment.Sagas.Models.AppraisalSagaState>()
//             .FirstOrDefaultAsync(s => s.RequestId == request.RequestId, cancellationToken);
//
//         if (sagaState == null)
//         {
//             return new GetWorkflowStatusResponse(
//                 request.RequestId,
//                 "NotFound",
//                 null,
//                 null,
//                 new List<WorkflowStepDto>(),
//                 DateTime.UtcNow
//             );
//         }
//
//         var workflowSteps = BuildWorkflowSteps(sagaState.CurrentState);
//
//         var workflowProgress = new WorkflowProgressNotificationDto(
//             sagaState.CorrelationId,
//             sagaState.RequestId,
//             sagaState.CurrentState,
//             sagaState.Assignee,
//             sagaState.AssignType,
//             workflowSteps,
//             sagaState.LastUpdatedAt ?? sagaState.StartedAt
//         );
//
//         return new GetWorkflowStatusResponse(
//             workflowProgress.RequestId,
//             workflowProgress.CurrentState,
//             workflowProgress.NextAssignee,
//             workflowProgress.NextAssigneeType,
//             workflowProgress.WorkflowSteps,
//             workflowProgress.UpdatedAt
//         );
//     }
//
//     private static List<WorkflowStepDto> BuildWorkflowSteps(string currentState)
//     {
//         var allStates = new[]
//         {
//             "AwaitingAssignment",
//             "Admin",
//             "AppraisalStaff", 
//             "AppraisalChecker",
//             "AppraisalVerifier"
//         };
//
//         var currentIndex = Array.IndexOf(allStates, currentState);
//         
//         return allStates.Select((state, index) => new WorkflowStepDto(
//             state,
//             IsCompleted: index < currentIndex,
//             IsCurrent: index == currentIndex,
//             AssignedTo: index == currentIndex ? "Current User" : null,
//             CompletedAt: index < currentIndex ? DateTime.UtcNow.AddHours(-index) : null
//         )).ToList();
//     }
// }