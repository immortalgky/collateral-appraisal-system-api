using Auth.Domain.Menu;
using FluentValidation;

namespace Auth.Application.Features.Menu.CreateMenuItem;

public class CreateMenuItemCommandValidator : AbstractValidator<CreateMenuItemCommand>
{
    public CreateMenuItemCommandValidator()
    {
        RuleFor(x => x.ItemKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Scope).NotEmpty()
            .Must(s => Enum.TryParse<MenuScope>(s, true, out _))
            .WithMessage("Scope must be one of: Main, Appraisal");
        RuleFor(x => x.IconName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.IconStyle).NotEmpty()
            .Must(s => Enum.TryParse<IconStyle>(s, true, out _))
            .WithMessage("IconStyle must be one of: Solid, Regular, Light, Duotone, Thin, Brands");
        RuleFor(x => x.IconColor).MaximumLength(100);
        RuleFor(x => x.Path).MaximumLength(500);
        RuleFor(x => x.ViewPermissionCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EditPermissionCode).MaximumLength(100);
        RuleFor(x => x.Translations).NotEmpty()
            .WithMessage("At least one translation is required");
        RuleFor(x => x.Translations)
            .Must(list => list == null || list
                .Select(t => (t.LanguageCode ?? string.Empty).Trim().ToLowerInvariant())
                .Distinct()
                .Count() == list.Count)
            .WithMessage("Translations must have unique language codes");
        RuleForEach(x => x.Translations).ChildRules(t =>
        {
            t.RuleFor(x => x.LanguageCode).NotEmpty().MaximumLength(10);
            t.RuleFor(x => x.Label).NotEmpty().MaximumLength(500);
        });
    }
}
