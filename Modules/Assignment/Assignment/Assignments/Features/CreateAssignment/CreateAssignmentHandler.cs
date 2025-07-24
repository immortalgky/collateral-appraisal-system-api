namespace Assignment.Assignments.Features.CreateAssignment;

internal class CreateAssignmentHandler(
    IAssignmentRepository assignmentRepository)
    : ICommandHandler<CreateAssignmentCommand, CreateAssignmentResult>
{
    public async Task<CreateAssignmentResult> Handle(CreateAssignmentCommand command, CancellationToken cancellationToken)
    {
        var request = CreateNewAssignment(command);

        await assignmentRepository.CreateAssignment(request, cancellationToken);

        return new CreateAssignmentResult(request.Id);
    }
     private static Models.Assignment CreateNewAssignment(CreateAssignmentCommand command)
    {
        var request = Models.Assignment.Create(
            command.RequestId,
            command.AssignmentMethod,
            command.ExternalCompanyId,
            command.ExternalCompanyAssignType,
            command.ExtApprStaff,
            command.ExtApprStaffAssignmentType,
            command.IntApprStaff,
            command.IntApprStaffAssignmentType,
            command.Remark
        );

        return request;
    }
}