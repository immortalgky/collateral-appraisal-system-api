namespace Request.Services;

public interface IAppraisalNumberGenerator
{
    Task<AppraisalNumber> GenerateAsync(CancellationToken cancellationToken = default);
}