namespace Auth.Domain.Companies;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Company?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<List<Company>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every company row, including soft-deleted ones. Only the seeder should need this: the
    /// global query filter on <see cref="Company"/> hides deleted rows, so a seeder that used
    /// <see cref="GetAllAsync"/> would not see a company an admin deleted and would re-insert it
    /// on every restart. Normal read paths must keep using GetAllAsync.
    /// </summary>
    Task<List<Company>> GetAllForSeedAsync(CancellationToken cancellationToken = default);
    Task<List<Company>> SearchAsync(string? searchTerm, bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<List<Company>> GetByLoanTypeAsync(string loanType, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task AddAsync(Company company, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
