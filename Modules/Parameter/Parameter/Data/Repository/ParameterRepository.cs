namespace Parameter.Data.Repository;

public class ParameterRepository(ParameterDbContext dbContext) : IParameterRepository
{
    public async Task<Parameters.Models.Parameter?> GetParameterByParId(
        long parId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Parameters
            .FirstOrDefaultAsync(p => p.Id == parId, cancellationToken);
    }

    public async Task<List<Parameters.Models.Parameter>> GetParameter(ParameterDto request, bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Parameters.AsQueryable();

        if (asNoTracking)
            query = query.AsNoTracking();

        query = query.WhereIf(request.ParId.HasValue && request.ParId.Value != 0, d => d.Id == request.ParId.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Group), d => d.Group == request.Group)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Country), d => d.Country == request.Country)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Language), d => d.Language == request.Language)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Code), d => d.Code == request.Code)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Description), d => d.Description == request.Description)
            .WhereIf(request.IsActive.HasValue, d => d.IsActive == request.IsActive)
            .WhereIf(request.SeqNo.HasValue, d => d.SeqNo == request.SeqNo);

        return await query.OrderBy(p => p.SeqNo).ToListAsync(cancellationToken);
    }
    public async Task AddAsync(
        Parameters.Models.Parameter parameter,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Parameters.AddAsync(parameter, cancellationToken);
    }
    public async Task DeleteAsync(
        long parId,
        CancellationToken cancellationToken = default)
    {
        var parameter = await dbContext.Parameters
            .FirstOrDefaultAsync(p => p.Id == parId, cancellationToken)
            ?? throw new InvalidOperationException($"Parameter Id: {parId} is not found.");
        dbContext.Parameters.Remove(parameter);
    } 
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
