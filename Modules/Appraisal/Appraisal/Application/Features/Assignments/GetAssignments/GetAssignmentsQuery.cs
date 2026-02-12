namespace Appraisal.Application.Features.Assignments.GetAssignments;

public record GetAssignmentsQuery(Guid AppraisalId) : IQuery<GetAssignmentsResult>;
