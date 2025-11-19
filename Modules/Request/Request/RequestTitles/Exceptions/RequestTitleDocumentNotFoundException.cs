namespace Request.RequestTitles.Exceptions;

public class RequestTitleDocumentNotFoundException(Guid id) : NotFoundException("RequestTitleDocument", id)
{
}