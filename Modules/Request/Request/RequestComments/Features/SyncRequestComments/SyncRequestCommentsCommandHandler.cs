using System;
using Request.RequestComments.Features.AddRequestComment;
using Request.RequestComments.Features.GetRequestCommentsByRequestId;
using Request.RequestComments.Features.RemoveRequestComment;
using Request.RequestComments.Features.UpdateRequestComment;

namespace Request.RequestComments.Features.SyncRequestComments;

public class SyncRequestCommentsCommandHandler(ISender sender) : ICommandHandler<SyncRequestCommentsCommand, SyncRequestCommentsResult>
{
    public async Task<SyncRequestCommentsResult> Handle(SyncRequestCommentsCommand command, CancellationToken cancellationToken)
    {
        var requestCommentsResult = await sender.Send(new GetRequestCommentsByRequestIdQuery(command.RequestId), cancellationToken);

        var requestComments = command.RequestCommentDtos ?? new List<RequestCommentDto>();
        var existingRequestComments = requestCommentsResult.Comments ?? new List<RequestCommentDto>();

        // Check New comments to add
        var newComments = requestComments 
            .Where(x => !existingRequestComments.Any(ec => ec.Id == x.Id))
            .ToList();

        // Check Comments to remove
        var commentsToRemove = existingRequestComments
            .Where(ec => ! requestComments.Any(rc => rc.Id == ec.Id))
            .ToList();

        // Check Comments to update
        var commentsToUpdate = requestComments 
            .Where(rc => existingRequestComments.Any(ec => ec.Id == rc.Id && (
                ec.Comment != rc.Comment ||
                ec.CommentedBy != rc.CommentedBy ||
                ec.CommentedAt != rc.CommentedAt
                )))
            .ToList();

        foreach (var comment in newComments)
        {
            await sender.Send(new AddRequestCommentCommand(command.RequestId, comment.Comment, comment.CommentedBy, comment.CommentedByName), cancellationToken);
        }

        foreach (var comment in commentsToRemove)
        {
            await sender.Send(new RemoveRequestCommentCommand(comment.Id!.Value), cancellationToken);
        }

        foreach (var comment in commentsToUpdate)
        {
            await sender.Send(new UpdateRequestCommentCommand(comment.Id!.Value, comment.Comment), cancellationToken);
        }
            
        return new SyncRequestCommentsResult(true);
    }
}
