using Shared.Exceptions;

namespace Request.Requests.Exceptions;

public class UploadDocumentException(string message) : BadRequestException(message)
{
}