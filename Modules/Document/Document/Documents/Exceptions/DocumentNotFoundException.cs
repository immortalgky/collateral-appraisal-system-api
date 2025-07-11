namespace Document.Documents.Exceptions;

public class DocumentNotFoundException(long id) : NotFoundException("Request", id)
{
}