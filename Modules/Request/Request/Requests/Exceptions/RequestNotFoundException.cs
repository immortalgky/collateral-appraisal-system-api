using Shared.Exceptions;

namespace Request.Requests.Exceptions;

public class RequestNotFoundException(Guid id) : NotFoundException("Request", id)
{
}