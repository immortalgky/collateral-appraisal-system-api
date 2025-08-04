namespace Request.Data.Repository;

public class RequestReadRepository : BaseReadRepository<Requests.Models.Request, long>, IRequestReadRepository
{
    public RequestReadRepository(RequestDbContext dbContext) : base(dbContext)
    {
    }
}