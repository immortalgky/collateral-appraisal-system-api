using Shared.Exceptions;

namespace Document.Documents.Exceptions;

public class UploadDocumentException(string message) : BadRequestException(message)
{
}