namespace Assignment.Assignments.Features.UpdateAssignment;

public class UpdateAssignmentCommandValidator : AbstractValidator<UpdateAssignmentCommand>
{
    public UpdateAssignmentCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty()
            .WithMessage("RequestId is required.");

        RuleFor(x => x.AssignmentMethod)
            .NotEmpty()
            .WithMessage("AssignmentMethod is required.");
    }
}