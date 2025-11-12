namespace Request.RequestTitles.Features.RemoveRequestTitle;

public class RemoveRequestTitleCommandValidator : AbstractValidator<RemoveRequestTitleCommand>
{
    public RemoveRequestTitleCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotEmpty()
            .WithMessage("{property} must not empty.");

        RuleFor(x => x.Id)
            .NotNull()
            .WithMessage("{property} must not empty.");
    }
}