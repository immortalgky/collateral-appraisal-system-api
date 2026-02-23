using Shared.Data;

namespace Appraisal.Domain.Appraisals;

public interface IAppraisalAppendixRepository : IRepository<AppraisalAppendix, Guid>
{
    Task<AppraisalAppendix?> GetByIdWithDocumentsAsync(
        Guid id, CancellationToken ct = default);

    Task<IEnumerable<AppraisalAppendix>> GetByAppraisalIdWithDocumentsAsync(
        Guid appraisalId, CancellationToken ct = default);
}
