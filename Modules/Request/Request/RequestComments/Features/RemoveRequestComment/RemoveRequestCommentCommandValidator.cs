namespace Request.RequestComments.Features.RemoveRequestComment;

public class RemoveRequestCommentCommandValidator : AbstractValidator<RemoveRequestCommentCommand>
{
    public RemoveRequestCommentCommandValidator()
    {
        RuleFor(x => x.CommentId)
            .NotNull()
            .WithMessage("Comment ID cannot be null.")
            .GreaterThan(0)
            .WithMessage("Comment ID must be greater than zero.");
    }
}