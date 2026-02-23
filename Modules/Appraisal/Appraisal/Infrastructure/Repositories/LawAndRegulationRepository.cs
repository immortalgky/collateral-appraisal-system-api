using Appraisal.Domain.Appraisals;
using Microsoft.EntityFrameworkCore;
using Shared.Data;

namespace Appraisal.Infrastructure.Repositories;

public class LawAndRegulationRepository(AppraisalDbContext dbContext)
    : BaseRepository<LawAndRegulation, Guid>(dbContext), ILawAndRegulationRepository
{
    private readonly AppraisalDbContext _dbContext = dbContext;

    public async Task<IEnumerable<LawAndRegulation>> GetByAppraisalIdWithImagesAsync(
        Guid appraisalId, CancellationToken ct = default)
    {
        return await _dbContext.LawAndRegulations
            .Include(l => l.Images)
            .Where(l => l.AppraisalId == appraisalId)
            .ToListAsync(ct);
    }
}
