namespace Assignment.Assignments.Features.GetAssignmentById;

public record GetAssignmentByIdQuery(long Id) : IQuery<GetAssignmentByIdResult>;

