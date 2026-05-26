namespace Parameter.Data.Repository;

public interface IParameterRepository
{
    Task<List<Parameters.Models.Parameter>> GetParameter(ParameterDto request, bool asNoTracking = true,
        CancellationToken cancellationToken = default);
    Task<Parameters.Models.Parameter?> GetParameterByParId(long parId,
        CancellationToken cancellationToken = default);
    Task AddAsync(Parameters.Models.Parameter parameter,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(long id,
        CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

}
