namespace Request.Domain.RequestTitles.Exceptions;

public class TitleDocumentNotFoundException(Guid id) : NotFoundException("TitleDocument", id)
{
}
