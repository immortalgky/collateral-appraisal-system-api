namespace Assignment.Assignments.Features.UpdateAssignment;

public class UpdateAssignmentCommandValidator : AbstractValidator<UpdateAssignmentCommand>
{
    public UpdateAssignmentCommandValidator()
    {
        RuleFor(x => x.ReqID)
            .NotEmpty()
            .WithMessage("ReqID is required.");

        RuleFor(x => x.AssignmentMethod)
            .NotEmpty()
            .WithMessage("AssignmentMethod is required.");
    }
}