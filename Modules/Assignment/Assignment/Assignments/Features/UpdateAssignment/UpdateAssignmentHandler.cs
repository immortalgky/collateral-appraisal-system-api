namespace Assignment.Assignments.Features.UpdateAssignment;

internal class UpdateAssignmentHandler(
    IAssignmentRepository assignmentRepository)
    : ICommandHandler<UpdateAssignmentCommand, UpdateAssignmentResult>
{
    public async Task<UpdateAssignmentResult> Handle(UpdateAssignmentCommand command, CancellationToken cancellationToken)
    {

        var request = Models.Assignment.UpdateDetailObject(
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
        bool result = await assignmentRepository.UpdateAssignment(command.Id,request, cancellationToken);
        if (result != true) throw new AssignmentNotFoundException(command.Id);
        return new UpdateAssignmentResult(result);
    }
}