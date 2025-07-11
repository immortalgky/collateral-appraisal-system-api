namespace Request.Requests.Features.UpdateRequestComment;

public class UpdateRequestCommentCommandValidator : AbstractValidator<UpdateRequestCommentCommand>
{
    public UpdateRequestCommentCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotNull()
            .WithMessage("Request ID cannot be null.")
            .GreaterThan(0)
            .WithMessage("Request ID must be greater than zero.");

        RuleFor(x => x.CommentId)
            .NotNull()
            .WithMessage("Comment ID cannot be null.")
            .GreaterThan(0)
            .WithMessage("Comment ID must be greater than zero.");

        RuleFor(x => x.Comment)
            .NotEmpty()
            .WithMessage("Comment cannot be empty.")
            .MaximumLength(250)
            .WithMessage("Comment cannot exceed 250 characters.");
    }
}