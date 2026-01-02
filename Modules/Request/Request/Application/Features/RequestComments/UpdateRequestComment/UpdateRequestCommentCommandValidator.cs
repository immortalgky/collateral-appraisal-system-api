namespace Request.Application.Features.RequestComments.UpdateRequestComment;

public class UpdateRequestCommentCommandValidator : AbstractValidator<UpdateRequestCommentCommand>
{
    public UpdateRequestCommentCommandValidator()
    {
        RuleFor(x => x.CommentId)
            .NotNull()
            .WithMessage("Comment ID cannot be null.");

        RuleFor(x => x.Comment)
            .NotEmpty()
            .WithMessage("Comment cannot be empty.");
    }
}