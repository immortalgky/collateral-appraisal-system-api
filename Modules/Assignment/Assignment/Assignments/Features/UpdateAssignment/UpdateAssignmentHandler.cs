namespace Assignment.Assignments.Features.UpdateAssignment;

internal class UpdateAssignmentHandler(AssignmentDbContext dbContext)
    : ICommandHandler<UpdateAssignmentCommand, UpdateAssignmentResult>
{
    public async Task<UpdateAssignmentResult> Handle(UpdateAssignmentCommand command, CancellationToken cancellationToken)
    {
        var request = await dbContext.Assignments.FindAsync([command.Id], cancellationToken);
        if (request is null) throw new AssignmentNotFoundException(command.Id);

        request.UpdateDetail(
            command.ReqID,
            command.AssignmentMethod,
            command.ExternalCompanyID,
            command.ExternalCompanyAssignType,
            command.ExtApprStaff,
            command.ExtApprStaffAssignmentType,
            command.IntApprStaff,
            command.IntApprStaffAssignmentType,
            command.Remark
        );

        await dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateAssignmentResult(true);
    }
}