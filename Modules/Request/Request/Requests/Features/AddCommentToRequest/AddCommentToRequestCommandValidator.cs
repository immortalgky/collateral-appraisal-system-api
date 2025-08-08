namespace Request.Requests.Features.AddCommentToRequest;

public class AddCommentToRequestCommandValidator : AbstractValidator<AddCommentToRequestCommand>
{
    public AddCommentToRequestCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotNull()
            .WithMessage("Request ID cannot be null.")
            .GreaterThan(0)
            .WithMessage("Request ID must be greater than 0.");

        RuleFor(x => x.Comment)
            .NotEmpty()
            .WithMessage("Comment cannot be empty.")
            .MaximumLength(250)
            .WithMessage("Comment cannot exceed 250 characters.");
    }
}