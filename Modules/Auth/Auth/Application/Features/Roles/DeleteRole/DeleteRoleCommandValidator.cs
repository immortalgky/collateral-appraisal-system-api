using FluentValidation;

namespace Auth.Domain.Roles.Features.DeleteRole;

public class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required.");
    }
}
