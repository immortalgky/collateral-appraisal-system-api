using FluentValidation;

namespace Auth.Application.Features.Menu.ReorderMenuItems;

public class ReorderMenuItemsCommandValidator : AbstractValidator<ReorderMenuItemsCommand>
{
    public ReorderMenuItemsCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one item is required");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.Id).NotEmpty();
        });
    }
}
