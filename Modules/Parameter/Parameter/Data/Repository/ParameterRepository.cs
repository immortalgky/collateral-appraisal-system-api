using Parameter.Parameters.Exceptions;

namespace Parameter.Data.Repository;

public class ParameterRepository(ParameterDbContext dbContext) : IParameterRepository
{
    public async Task<List<Parameters.Models.Parameter>> GetParameters(CancellationToken cancellationToken = default)
    {
        var result = await dbContext.Parameters.ToListAsync(cancellationToken);

        return result;
    }

    public async Task<List<Parameters.Models.Parameter>> GetParameter(ParameterDto request, bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Parameters.AsQueryable();

        query = query.WhereIf(request.ParId.HasValue && request.ParId.Value != 0, d => d.Id == request.ParId.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Group), d => d.Group == request.Group)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Country), d => d.Group == request.Country)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Language), d => d.Group == request.Language)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Code), d => d.Group == request.Code)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Description), d => d.Group == request.Description)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Active), d => d.Group == request.Active)
            .WhereIf(!string.IsNullOrWhiteSpace(request.SeqNo), d => d.Group == request.SeqNo);

        return await query.ToListAsync(cancellationToken);
    }
}