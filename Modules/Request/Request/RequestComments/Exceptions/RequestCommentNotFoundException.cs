namespace Request.RequestComments.Exceptions;

public class RequestCommentNotFoundException(Guid id) : NotFoundException("RequestComment", id)
{
}