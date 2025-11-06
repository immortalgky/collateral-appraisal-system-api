namespace Request.RequestComments.Exceptions;

public class RequestCommentNotFoundException(long id) : NotFoundException("RequestComment", id)
{
}