using Appraisal.Domain.Projects;
using FluentValidation;

namespace Appraisal.Application.Features.BlockUnitMaintenance.UpdateProjectUnitSaleInfo;

public class UpdateProjectUnitSaleInfoCommandValidator
    : AbstractValidator<UpdateProjectUnitSaleInfoCommand>
{
    public UpdateProjectUnitSaleInfoCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("ProjectId is required.");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("Items list is required.")
            .NotEmpty().WithMessage("At least one unit must be provided.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.UnitId)
                .NotEmpty().WithMessage("UnitId is required.");

            // When sold, PurchaseBy must be provided.
            item.When(i => i.IsSold, () =>
            {
                item.RuleFor(i => i.PurchaseBy)
                    .NotNull().WithMessage("PurchaseBy is required when the unit is sold.");
            });

            // When PurchaseBy = Loan, LoanBankName must be non-empty.
            item.When(i => i.IsSold && i.PurchaseBy == UnitPurchaseMethod.Loan, () =>
            {
                item.RuleFor(i => i.LoanBankName)
                    .NotEmpty().WithMessage("LoanBankName is required when PurchaseBy is Loan.");
            });
        });
    }
}
