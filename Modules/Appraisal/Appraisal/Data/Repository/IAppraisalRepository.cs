namespace Appraisal.Data.Repository;

public interface IAppraisalRepository
{
    Task<long> CreateLandAppraisalDetails(LandAppraisalDetail appraisal, CancellationToken cancellationToken);
}