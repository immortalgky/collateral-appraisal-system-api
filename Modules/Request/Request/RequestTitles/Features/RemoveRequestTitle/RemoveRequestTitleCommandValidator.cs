namespace Request.RequestTitles.Features.RemoveRequestTitle;

public class RemoveRequestTitleCommandValidator : AbstractValidator<RemoveRequestTitleCommand>
{
    public RemoveRequestTitleCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .GreaterThan(0).WithMessage("RequestId must be greater than 0.");

        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be greater than 0.");
    }
}