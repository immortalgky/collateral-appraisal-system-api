namespace Request.Application.Features.RequestComments.AddRequestComment;

public class AddRequestCommentCommandValidator : AbstractValidator<AddRequestCommentCommand>
{
    public AddRequestCommentCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .NotNull()
            .WithMessage("Request ID cannot be null.");

        RuleFor(x => x.Comment)
            .NotEmpty()
            .WithMessage("Comment cannot be empty.");

        RuleFor(x => x.CommentedBy)
            .NotNull()
            .WithMessage("CommentedBy cannot be null.")
            .MaximumLength(10)
            .WithMessage("Comment cannot exceed 10 characters.");

        RuleFor(x => x.CommentedByName)
            .NotNull()
            .WithMessage("CommentedByName cannot be null")
            .MaximumLength(100)
            .WithMessage("Comment cannot exceed 100 characters.");
    }
}