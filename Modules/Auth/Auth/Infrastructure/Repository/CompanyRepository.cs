using Auth.Domain.Companies;

namespace Auth.Infrastructure.Repository;

public class CompanyRepository(AuthDbContext dbContext) : ICompanyRepository
{
    public async Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Companies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Company?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await dbContext.Companies.FirstOrDefaultAsync(c => c.Name == name, cancellationToken);
    }

    public async Task<List<Company>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        // Read-only callers (selection, listing), so skip change tracking. Seeding uses
        // GetAllForSeedAsync below — it needs the soft-deleted rows this one hides.
        var query = dbContext.Companies.AsNoTracking();
        if (activeOnly)
            query = query.Where(c => c.IsActive);
        return await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public async Task<List<Company>> GetAllForSeedAsync(CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters so soft-deleted rows are included: AuthDbContext filters them out
        // globally, and without them the seeder cannot tell "never seeded" from "seeded, then
        // deleted by an admin" — it would resurrect the deleted company on every restart.
        return await dbContext.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Company>> SearchAsync(string? searchTerm, bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Companies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(c =>
                c.Name.Contains(searchTerm)
                || (c.NameLocal != null && c.NameLocal.Contains(searchTerm))
                || (c.TaxId != null && c.TaxId.Contains(searchTerm)));

        if (activeOnly)
            query = query.Where(c => c.IsActive);

        return await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public async Task<List<Company>> GetByLoanTypeAsync(string loanType, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Companies.AsNoTracking().Where(c => !c.IsDeleted);

        if (activeOnly)
            query = query.Where(c => c.IsActive);

        var companies = await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);

        // Filter by LoanType in-memory since it's a JSON column
        return companies
            .Where(c => c.LoanTypes.Contains(loanType, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        await dbContext.Companies.AddAsync(company, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
