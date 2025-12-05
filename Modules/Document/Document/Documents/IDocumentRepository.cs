using Shared.Data;

namespace Document.Documents;

public interface IDocumentRepository : IRepository<Models.Document, Guid>
{
}