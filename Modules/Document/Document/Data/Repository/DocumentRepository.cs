using Document.Documents;
using Shared.Data;

namespace Document.Data.Repository;

public class DocumentRepository(DocumentDbContext dbContext)
    : BaseRepository<Documents.Models.Document, Guid>(dbContext), IDocumentRepository;