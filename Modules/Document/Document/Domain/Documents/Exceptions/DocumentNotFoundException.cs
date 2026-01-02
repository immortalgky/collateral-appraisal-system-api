namespace Document.Domain.Documents.Exceptions;

public class DocumentNotFoundException(long id) : NotFoundException("Request", id)
{
}