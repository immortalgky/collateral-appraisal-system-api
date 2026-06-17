using FluentValidation;

namespace Auth.Application.Features.Companies.UpdateCompany;

public class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameLocal).MaximumLength(200);
        RuleFor(x => x.TaxId).MaximumLength(50);
        RuleFor(x => x.Phone).MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.AddressLine1).MaximumLength(500);
        RuleFor(x => x.AddressLine2).MaximumLength(500);
        RuleFor(x => x.ContactPerson).MaximumLength(200);
        RuleFor(x => x.HostCompanyCode).MaximumLength(10);
        RuleFor(x => x.BankAccountNo).MaximumLength(20);
        RuleFor(x => x.BankAccountName).MaximumLength(200);
        // Friendly field-level 400 for the common case. Company.Create/Update also enforces this as a
        // domain invariant (DomainException → 400) so non-API writers (seeder, imports) can't bypass it.
        // Compared date-only to match Company.IsAssignable (which evaluates the window by calendar day),
        // so a legitimate same-day window with any time-of-day is not rejected.
        RuleFor(x => x.ExpireDate)
            .Must((cmd, _) => cmd.EffectiveDate is null || cmd.ExpireDate is null
                || cmd.EffectiveDate.Value.Date <= cmd.ExpireDate.Value.Date)
            .WithMessage("Effective date must be on or before expire date.");
    }
}
