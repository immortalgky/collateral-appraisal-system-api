namespace Assignment.Assignments.Features.CreateAssignment;

public class CreateAssignmentCommandValidator : AbstractValidator<CreateAssignmentCommand>
{
    public CreateAssignmentCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty()
            .WithMessage("RequestId is required.");

        RuleFor(x => x.AssignmentMethod)
            .NotEmpty()
            .WithMessage("AssignmentMethod is required.");

    }
}