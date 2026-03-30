using Auth.Domain.Companies;

namespace Workflow.AssigneeSelection.Services;

public class CompanyRoundRobinService : ICompanyRoundRobinService
{
    private const string ActivityName = "CompanyRouting";
    private const string GroupKey = "AllCompanies";

    private readonly ICompanyRepository _companyRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IGroupHashService _groupHashService;
    private readonly ILogger<CompanyRoundRobinService> _logger;

    public CompanyRoundRobinService(
        ICompanyRepository companyRepository,
        IAssignmentRepository assignmentRepository,
        IGroupHashService groupHashService,
        ILogger<CompanyRoundRobinService> logger)
    {
        _companyRepository = companyRepository;
        _assignmentRepository = assignmentRepository;
        _groupHashService = groupHashService;
        _logger = logger;
    }

    public async Task<CompanySelectionResult> SelectCompanyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var companies = await _companyRepository.GetAllAsync(activeOnly: true, cancellationToken);

            if (companies.Count == 0)
            {
                _logger.LogWarning("No active companies found for round-robin routing");
                return CompanySelectionResult.Failure("No active companies available for assignment");
            }

            var companyIds = companies.Select(c => c.Id.ToString()).ToList();
            var groupsHash = _groupHashService.GenerateGroupsHash([GroupKey]);
            var groupsList = _groupHashService.GenerateGroupsList([GroupKey]);

            await _assignmentRepository.SyncUsersForGroupCombinationAsync(
                ActivityName,
                groupsHash,
                groupsList,
                companyIds,
                cancellationToken);

            var selectedId = await _assignmentRepository.SelectNextUserWithRoundResetAsync(
                ActivityName,
                groupsHash,
                cancellationToken);

            if (selectedId == null)
            {
                _logger.LogWarning("Round-robin returned no company selection");
                return CompanySelectionResult.Failure("Round-robin selection returned no result");
            }

            var selectedCompany = companies.FirstOrDefault(c => c.Id.ToString() == selectedId);
            if (selectedCompany == null)
            {
                _logger.LogWarning("Selected company ID {CompanyId} not found in active companies", selectedId);
                return CompanySelectionResult.Failure($"Selected company {selectedId} not found");
            }

            _logger.LogInformation(
                "Company round-robin selected {CompanyName} ({CompanyId}) from {TotalCompanies} active companies",
                selectedCompany.Name, selectedCompany.Id, companies.Count);

            return CompanySelectionResult.Success(selectedCompany.Id, selectedCompany.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select company via round-robin");
            return CompanySelectionResult.Failure($"Company selection failed: {ex.Message}");
        }
    }

    public async Task<CompanySelectionResult> SelectCompanyAsync(string loanType, CancellationToken cancellationToken = default)
    {
        try
        {
            var companies = await _companyRepository.GetByLoanTypeAsync(loanType, activeOnly: true, cancellationToken);

            if (companies.Count == 0)
            {
                _logger.LogWarning("No active companies found for loan type {LoanType}", loanType);
                return CompanySelectionResult.Failure($"No active companies available for loan type '{loanType}'");
            }

            var groupKey = $"LoanType_{loanType}";
            var companyIds = companies.Select(c => c.Id.ToString()).ToList();
            var groupsHash = _groupHashService.GenerateGroupsHash([groupKey]);
            var groupsList = _groupHashService.GenerateGroupsList([groupKey]);

            await _assignmentRepository.SyncUsersForGroupCombinationAsync(
                ActivityName,
                groupsHash,
                groupsList,
                companyIds,
                cancellationToken);

            var selectedId = await _assignmentRepository.SelectNextUserWithRoundResetAsync(
                ActivityName,
                groupsHash,
                cancellationToken);

            if (selectedId == null)
            {
                _logger.LogWarning("Round-robin returned no company selection for loan type {LoanType}", loanType);
                return CompanySelectionResult.Failure("Round-robin selection returned no result");
            }

            var selectedCompany = companies.FirstOrDefault(c => c.Id.ToString() == selectedId);
            if (selectedCompany == null)
            {
                _logger.LogWarning("Selected company ID {CompanyId} not found for loan type {LoanType}", selectedId, loanType);
                return CompanySelectionResult.Failure($"Selected company {selectedId} not found");
            }

            _logger.LogInformation(
                "Company round-robin selected {CompanyName} ({CompanyId}) from {TotalCompanies} companies for loan type {LoanType}",
                selectedCompany.Name, selectedCompany.Id, companies.Count, loanType);

            return CompanySelectionResult.Success(selectedCompany.Id, selectedCompany.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select company via round-robin for loan type {LoanType}", loanType);
            return CompanySelectionResult.Failure($"Company selection failed: {ex.Message}");
        }
    }
}
