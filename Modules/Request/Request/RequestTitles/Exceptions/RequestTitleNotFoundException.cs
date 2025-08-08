using Shared.Exceptions;

namespace Request.RequestTitles.Exceptions;

public class RequestTitleNotFoundException(long id) : NotFoundException("RequestTitle", id)
{
}