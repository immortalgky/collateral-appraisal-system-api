using Shared.Data;

namespace Appraisal.Domain.Appraisals;

public interface IAppraisalDocumentRepository : IRepository<AppraisalDocument, Guid>
{
    Task<AppraisalDocument?> GetByIdAndAppraisalIdAsync(
        Guid id, Guid appraisalId, CancellationToken ct = default);
}
