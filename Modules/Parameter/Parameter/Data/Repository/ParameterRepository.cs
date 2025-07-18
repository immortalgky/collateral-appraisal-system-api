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

        if (request.ParId.HasValue && request.ParId.Value != 0)
            query = query.Where(d => d.Id == request.ParId.Value);

        if (!string.IsNullOrWhiteSpace(request.Group))
            query = query.Where(d => d.Group == request.Group);

        if (!string.IsNullOrWhiteSpace(request.Country))
            query = query.Where(d => d.Country == request.Country);

        if (!string.IsNullOrWhiteSpace(request.Language))
            query = query.Where(d => d.Language == request.Language);

        if (!string.IsNullOrWhiteSpace(request.Code))
            query = query.Where(d => d.Code == request.Code);

        if (!string.IsNullOrWhiteSpace(request.Description))
            query = query.Where(d => d.Description == request.Description);

        if (!string.IsNullOrWhiteSpace(request.Active))
            query = query.Where(d => d.Active == request.Active);

        if (!string.IsNullOrWhiteSpace(request.SeqNo))
            query = query.Where(d => d.SeqNo == request.SeqNo);

        var result = await query.ToListAsync(cancellationToken);

        return result;
    }
}