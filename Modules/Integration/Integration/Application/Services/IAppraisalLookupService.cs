namespace Integration.Application.Services;

public record AppraisalKeys(string? AppraisalNumber, string? ExternalCaseKey);

public interface IAppraisalLookupService
{
    Task<AppraisalKeys?> GetKeysAsync(Guid appraisalId, CancellationToken ct = default);
}
