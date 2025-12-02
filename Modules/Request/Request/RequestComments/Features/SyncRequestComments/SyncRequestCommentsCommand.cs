using System;

namespace Request.RequestComments.Features.SyncRequestComments;

public record SyncRequestCommentsCommand : ICommand<SyncRequestCommentsResult>
{
    public Guid RequestId { get; init; }
    public List<RequestCommentDto> RequestCommentDtos { get; init; } = new();
}
