namespace Request.Data.Repository;

public class RequestReadRepository : BaseReadRepository<Requests.Models.Request, Guid>, IRequestReadRepository
{
    public RequestReadRepository(RequestDbContext dbContext) : base(dbContext)
    {
    }
}