using Shared.Exceptions;

namespace Request.Domain.Requests.Exceptions;

public class RequestNotFoundException(Guid id) : NotFoundException("Request", id)
{
}