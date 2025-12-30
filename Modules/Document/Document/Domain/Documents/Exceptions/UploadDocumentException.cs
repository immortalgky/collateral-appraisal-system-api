using Shared.Exceptions;

namespace Document.Domain.Documents.Exceptions;

public class UploadDocumentException(string message) : BadRequestException(message)
{
}