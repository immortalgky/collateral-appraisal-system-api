namespace Request.Requests.Services;

public interface IAppraisalNumberGenerator
{
    Task<AppraisalNumber> GenerateAsync(CancellationToken cancellationToken = default);
}