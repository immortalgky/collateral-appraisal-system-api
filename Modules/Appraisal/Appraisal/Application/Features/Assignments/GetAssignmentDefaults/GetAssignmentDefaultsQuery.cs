namespace Appraisal.Application.Features.Assignments.GetAssignmentDefaults;

public record GetAssignmentDefaultsQuery(Guid AppraisalId) : IQuery<GetAssignmentDefaultsResult>;
