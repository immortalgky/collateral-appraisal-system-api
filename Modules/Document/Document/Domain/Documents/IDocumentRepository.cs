using System.Linq.Expressions;
using Shared.Data;

namespace Document.Domain.Documents;

public interface IDocumentRepository : IRepository<Models.Document, Guid>
{
    Task<IEnumerable<Models.Document>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Expression<Func<Models.Document, bool>> predicate, CancellationToken cancellationToken = default);
}