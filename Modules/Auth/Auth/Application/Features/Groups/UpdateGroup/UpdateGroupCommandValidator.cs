using FluentValidation;

namespace Auth.Application.Features.Groups.UpdateGroup;

public class UpdateGroupCommandValidator : AbstractValidator<UpdateGroupCommand>
{
    public UpdateGroupCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Scope).NotEmpty().Must(s => s == "Bank" || s == "Company")
            .WithMessage("Scope must be 'Bank' or 'Company'.");
    }
}
