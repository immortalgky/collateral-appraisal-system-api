namespace Assignment.Assignments.Features.GetAssignmentById;

internal class GetRequestByIdHandler(IAssignmentRepository assignmentRepository)
    : IQueryHandler<GetAssignmentByIdQuery, GetAssignmentByIdResult>
{
    public async Task<GetAssignmentByIdResult> Handle(GetAssignmentByIdQuery query, CancellationToken cancellationToken)
    {
        var request = await assignmentRepository.GetAssignmentById(query.Id,true, cancellationToken);
        if (request is null) throw new AssignmentNotFoundException(query.Id);
        var result = request.Adapt<AssignmentDetailDto>();

        return new GetAssignmentByIdResult(result);
    }
}