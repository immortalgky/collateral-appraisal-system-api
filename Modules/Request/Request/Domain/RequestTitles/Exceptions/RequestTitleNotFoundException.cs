using Shared.Exceptions;

namespace Request.Domain.RequestTitles.Exceptions;

public class RequestTitleNotFoundException(Guid id) : NotFoundException("RequestTitle", id)
{
}