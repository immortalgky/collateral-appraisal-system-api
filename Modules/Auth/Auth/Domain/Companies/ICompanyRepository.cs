namespace Auth.Domain.Companies;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Company?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<List<Company>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<List<Company>> SearchAsync(string? searchTerm, bool activeOnly = false, CancellationToken cancellationToken = default);
    Task AddAsync(Company company, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
