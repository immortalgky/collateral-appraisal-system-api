using Auth.Domain.Companies;
using Shared.Time;
using Workflow.Services.Configuration;

namespace Workflow.AssigneeSelection.Services;

public class CompanyRoundRobinService : ICompanyRoundRobinService
{
    private const string ActivityName = "CompanyRouting";
    private const string GroupKey = "AllCompanies";

    private readonly ICompanyRepository _companyRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IGroupHashService _groupHashService;
    private readonly ICompanyRoundRobinConfigService _configService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CompanyRoundRobinService> _logger;

    public CompanyRoundRobinService(
        ICompanyRepository companyRepository,
        IAssignmentRepository assignmentRepository,
        IGroupHashService groupHashService,
        ICompanyRoundRobinConfigService configService,
        IDateTimeProvider dateTimeProvider,
        ILogger<CompanyRoundRobinService> logger)
    {
        _companyRepository = companyRepository;
        _assignmentRepository = assignmentRepository;
        _groupHashService = groupHashService;
        _configService = configService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<CompanySelectionResult> SelectCompanyAsync(
        Guid? excludedCompanyId,
        string? loanType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var companies = string.IsNullOrEmpty(loanType)
                ? await _companyRepository.GetAllAsync(activeOnly: true, cancellationToken)
                : await _companyRepository.GetByLoanTypeAsync(loanType, activeOnly: true, cancellationToken);

            // Drop the excluded company and any whose MOU approval window is not current (single pass).
            // Use the shared application clock so this matches the activity's assignability check.
            var now = _dateTimeProvider.ApplicationNow;
            companies = companies
                .Where(c => c.IsAssignable(now)
                            && (!excludedCompanyId.HasValue || c.Id != excludedCompanyId.Value))
                .ToList();

            // Restrict to the admin-configured pool (and pick up per-company weights) when an active
            // pool exists for this scope. With no config, fall back to all active companies (weight 1).
            IReadOnlyDictionary<string, int>? weights = null;
            var poolConfig = await _configService.ResolveAsync(loanType, cancellationToken);
            if (poolConfig is not null && poolConfig.Entries.Count > 0)
            {
                // Entries are persisted unique-per-company with weight >= 1 (enforced on write); group
                // defensively so a row written outside that path (manual SQL, migration) can't throw a
                // duplicate-key exception here and silently block all routing.
                var weightById = poolConfig.Entries
                    .GroupBy(e => e.CompanyId)
                    .ToDictionary(g => g.Key, g => g.First().Weight);

                companies = companies.Where(c => weightById.ContainsKey(c.Id)).ToList();
                weights = companies.ToDictionary(c => c.Id.ToString(), c => weightById[c.Id]);
            }

            if (companies.Count == 0)
            {
                _logger.LogWarning(
                    "No eligible companies found for round-robin (excludedCompanyId={ExcludedId}, loanType={LoanType})",
                    excludedCompanyId, loanType);
                var msg = string.IsNullOrEmpty(loanType)
                    ? "No active companies available for assignment"
                    : $"No active companies available for loan type '{loanType}'";
                return CompanySelectionResult.Failure(msg);
            }

            var groupKey = string.IsNullOrEmpty(loanType) ? GroupKey : $"LoanType_{loanType}";
            var companyIds = companies.Select(c => c.Id.ToString()).ToList();
            var groupsHash = _groupHashService.GenerateGroupsHash([groupKey]);
            var groupsList = _groupHashService.GenerateGroupsList([groupKey]);

            await _assignmentRepository.SyncUsersForGroupCombinationAsync(
                ActivityName, groupsHash, groupsList, companyIds, cancellationToken, weights);

            var selectedId = await _assignmentRepository.SelectNextUserWithRoundResetAsync(
                ActivityName, groupsHash, cancellationToken);

            if (selectedId == null)
            {
                _logger.LogWarning("Round-robin returned no company selection (excludedCompanyId={ExcludedId}, loanType={LoanType})",
                    excludedCompanyId, loanType);
                return CompanySelectionResult.Failure("Round-robin selection returned no result");
            }

            var selectedCompany = companies.FirstOrDefault(c => c.Id.ToString() == selectedId);
            if (selectedCompany == null)
            {
                _logger.LogWarning("Selected company ID {CompanyId} not found in eligible companies", selectedId);
                return CompanySelectionResult.Failure($"Selected company {selectedId} not found");
            }

            _logger.LogInformation(
                "Company round-robin selected {CompanyName} ({CompanyId}) from {TotalCompanies} eligible companies (loanType={LoanType}, excluded={ExcludedId}, weighted={Weighted})",
                selectedCompany.Name, selectedCompany.Id, companies.Count, loanType ?? "any", excludedCompanyId, weights != null);

            return CompanySelectionResult.Success(selectedCompany.Id, selectedCompany.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select company via round-robin (excludedCompanyId={ExcludedId}, loanType={LoanType})",
                excludedCompanyId, loanType);
            return CompanySelectionResult.Failure($"Company selection failed: {ex.Message}");
        }
    }
}
