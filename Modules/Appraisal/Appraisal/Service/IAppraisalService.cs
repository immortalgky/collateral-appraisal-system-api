namespace Appraisal.Service;

public interface IAppraisalService
{
    RequestAppraisal CreateRequestAppraisalDetail(RequestAppraisalDto appraisal, long reqId, long collatId);
    TypeAdapterConfig CreateRequestAppraisalDetailConfig();
    Task<RequestAppraisal> UpdateRequestAppraisalDetail(RequestAppraisalDto appraisalDto, long id, CancellationToken cancellationToken = default);
    Task AddRequestAppraisalDetailAsync(RequestAppraisal appraisal, CancellationToken cancellationToken = default!);
    Task UpdateRequestAppraisalDetailAsync(RequestAppraisal appraisal, CancellationToken cancellationToken = default!);
    Task<PaginatedResult<RequestAppraisal>> GetRequestAppraisalDetailAsync(PaginationRequest pagination, CancellationToken cancellationToken = default!);
    Task<RequestAppraisal> GetRequestAppraisalDetailByIdAsync(long id, CancellationToken cancellationToken = default!);
    Task<List<RequestAppraisal>> GetRequestAppraisalDetailByCollateralIdAsync(long collatId, CancellationToken cancellationToken = default!);
    Task DeleteRequestAppraisalDetailAsync(long id, CancellationToken cancellationToken = default!);
}
