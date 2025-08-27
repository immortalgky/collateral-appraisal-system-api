namespace Appraisal.Data.Repository;

public class AppraisalRepository(AppraisalDbContext dbContext) : IAppraisalRepository
{
    public async Task<long> CreateLandAppraisalDetails(LandAppraisalDetail appraisal, CancellationToken cancellationToken = default)
    {
        dbContext.LandAppraisalDetails.Add(appraisal);
        await dbContext.SaveChangesAsync(cancellationToken);

        return appraisal.ApprId;
    }
}