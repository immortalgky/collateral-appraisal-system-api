namespace Request.Application.Services;

public interface IAppraisalNumberGenerator
{
    Task<AppraisalNumber> GenerateAsync(CancellationToken cancellationToken = default);
}