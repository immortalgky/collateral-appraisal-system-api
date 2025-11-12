using Shared.Exceptions;

namespace Request.RequestTitles.Exceptions;

public class RequestTitleNotFoundException(Guid id) : NotFoundException("RequestTitle", id)
{
}