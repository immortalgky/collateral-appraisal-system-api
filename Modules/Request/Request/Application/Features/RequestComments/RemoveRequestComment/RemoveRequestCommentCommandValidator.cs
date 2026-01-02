namespace Request.Application.Features.RequestComments.RemoveRequestComment;

public class RemoveRequestCommentCommandValidator : AbstractValidator<RemoveRequestCommentCommand>
{
    public RemoveRequestCommentCommandValidator()
    {
        RuleFor(x => x.CommentId)
            .NotNull()
            .WithMessage("Comment ID cannot be null.");
    }
}