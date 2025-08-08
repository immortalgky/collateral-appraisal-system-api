namespace Request.RequestTitles.Features.AddRequestTitle;

public class AddRequestTitleCommandValidator : AbstractValidator<AddRequestTitleCommand>
{
    public AddRequestTitleCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .GreaterThan(0).WithMessage("RequestId must be greater than 0.");

        RuleFor(x => x.CollateralType)
            .NotEmpty().WithMessage("CollateralType is required.");

        RuleFor(x => x.TitleAddress)
            .NotNull().WithMessage("TitleAddress is required.");

        RuleFor(x => x.Rai)
            .GreaterThanOrEqualTo(0).WithMessage("Rai must be greater than or equal to 0.")
            .When(x => x.Rai.HasValue);

        RuleFor(x => x.Ngan)
            .GreaterThanOrEqualTo(0).WithMessage("Ngan must be greater than or equal to 0.")
            .When(x => x.Ngan.HasValue);

        RuleFor(x => x.Wa)
            .GreaterThanOrEqualTo(0).WithMessage("Wa must be greater than or equal to 0.")
            .When(x => x.Wa.HasValue);

        RuleFor(x => x.UsageArea)
            .GreaterThan(0).WithMessage("UsageArea must be greater than 0.")
            .When(x => x.UsageArea.HasValue);

        RuleFor(x => x.NoOfBuilding)
            .GreaterThan(0).WithMessage("NoOfBuilding must be greater than 0.")
            .When(x => x.NoOfBuilding.HasValue);

        RuleFor(x => x.NoOfMachine)
            .GreaterThan(0).WithMessage("NoOfMachine must be greater than 0.")
            .When(x => x.NoOfMachine.HasValue);
    }
}