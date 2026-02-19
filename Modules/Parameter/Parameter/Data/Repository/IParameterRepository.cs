namespace Parameter.Data.Repository;

public interface IParameterRepository
{
    Task<List<Parameters.Models.Parameter>> GetParameter(ParameterDto request, bool asNoTracking = true,
        CancellationToken cancellationToken = default);
}
