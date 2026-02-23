using Shared.Data;

namespace Appraisal.Domain.Appraisals;

public interface ILawAndRegulationRepository : IRepository<LawAndRegulation, Guid>
{
    Task<IEnumerable<LawAndRegulation>> GetByAppraisalIdWithImagesAsync(Guid appraisalId, CancellationToken ct = default);
}
