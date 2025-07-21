namespace Assignment.Assignments.Features.CreateAssignment;

public class CreateAssignmentCommandValidator : AbstractValidator<CreateAssignmentCommand>
{
    public CreateAssignmentCommandValidator()
    {
        RuleFor(x => x.RequestID)
            .NotEmpty()
            .WithMessage("RequestID is required.");

        RuleFor(x => x.AssignmentMethod)
            .NotEmpty()
            .WithMessage("AssignmentMethod is required.");

    }
}