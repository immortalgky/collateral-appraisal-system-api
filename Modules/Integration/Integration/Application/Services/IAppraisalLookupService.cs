namespace Integration.Application.Services;

public record AppraisalKeys(string? AppraisalNumber, string? ExternalCaseKey, string? ExternalSystem);

public interface IAppraisalLookupService
{
    Task<AppraisalKeys?> GetKeysAsync(Guid appraisalId, CancellationToken ct = default);
    Task<AppraisalKeys?> GetKeysByRequestIdAsync(Guid requestId, CancellationToken ct = default);
}
