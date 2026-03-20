namespace Workflow.AssigneeSelection.Services;

public interface ICompanyRoundRobinService
{
    Task<CompanySelectionResult> SelectCompanyAsync(CancellationToken cancellationToken = default);
}

public record CompanySelectionResult(bool IsSuccess, Guid? CompanyId, string? CompanyName, string? ErrorMessage)
{
    public static CompanySelectionResult Success(Guid companyId, string companyName) =>
        new(true, companyId, companyName, null);

    public static CompanySelectionResult Failure(string errorMessage) =>
        new(false, null, null, errorMessage);
}
