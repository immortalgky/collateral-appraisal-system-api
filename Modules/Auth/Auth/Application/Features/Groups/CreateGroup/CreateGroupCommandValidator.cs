using FluentValidation;

namespace Auth.Application.Features.Groups.CreateGroup;

public class CreateGroupCommandValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Scope).NotEmpty().Must(s => s == "Bank" || s == "Company")
            .WithMessage("Scope must be 'Bank' or 'Company'.");
    }
}
