namespace Request.RequestTitles.Features.UpdateRequestTitle;

public class UpdateRequestTitleCommandValidator : AbstractValidator<UpdateRequestTitleCommand>
{
    public UpdateRequestTitleCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .GreaterThan(0).WithMessage("RequestId must be greater than 0.");

        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be greater than 0.");

        RuleFor(x => x.CollateralType)
            .NotEmpty().WithMessage("CollateralType is required.");

        RuleFor(x => x.TitleAddress)
            .NotNull().WithMessage("TitleAddress is required.");
    }
}