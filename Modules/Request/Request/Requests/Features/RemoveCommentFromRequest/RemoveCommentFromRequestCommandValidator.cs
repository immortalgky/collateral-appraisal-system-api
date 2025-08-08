namespace Request.Requests.Features.RemoveCommentFromRequest;

public class RemoveCommentFromRequestCommandValidator : AbstractValidator<RemoveCommentFromRequestCommand>
{
    public RemoveCommentFromRequestCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotNull()
            .WithMessage("Request ID cannot be null.")
            .GreaterThan(0)
            .WithMessage("Request ID must be greater than zero.");
    }
}