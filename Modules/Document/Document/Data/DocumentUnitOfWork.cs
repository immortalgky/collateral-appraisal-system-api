using Shared.Data;

namespace Document.Data;

public class DocumentUnitOfWork(DocumentDbContext dbContext, IServiceProvider sp)
    : UnitOfWork<DocumentDbContext>(dbContext, sp), IDocumentUnitOfWork;