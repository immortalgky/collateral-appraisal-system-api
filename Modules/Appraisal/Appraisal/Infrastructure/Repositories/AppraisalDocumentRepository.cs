using Appraisal.Domain.Appraisals;
using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace Appraisal.Infrastructure.Repositories;

public class AppraisalDocumentRepository(AppraisalDbContext dbContext)
    : BaseRepository<AppraisalDocument, Guid>(dbContext), IAppraisalDocumentRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    public async Task<AppraisalDocument?> GetByIdAndAppraisalIdAsync(
        Guid id, Guid appraisalId, CancellationToken ct = default)
    {
        return await _dbContext.AppraisalDocuments
            .FirstOrDefaultAsync(d => d.Id == id && d.AppraisalId == appraisalId, ct);
    }
}
