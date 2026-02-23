using Appraisal.Domain.Appraisals;
using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace Appraisal.Infrastructure.Repositories;

public class AppraisalAppendixRepository(AppraisalDbContext dbContext)
    : BaseRepository<AppraisalAppendix, Guid>(dbContext), IAppraisalAppendixRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    public async Task<AppraisalAppendix?> GetByIdWithDocumentsAsync(
        Guid id, CancellationToken ct = default)
    {
        return await _dbContext.AppraisalAppendices
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<IEnumerable<AppraisalAppendix>> GetByAppraisalIdWithDocumentsAsync(
        Guid appraisalId, CancellationToken ct = default)
    {
        return await _dbContext.AppraisalAppendices
            .Include(a => a.Documents)
            .Where(a => a.AppraisalId == appraisalId)
            .OrderBy(a => a.SortOrder)
            .ToListAsync(ct);
    }
}
