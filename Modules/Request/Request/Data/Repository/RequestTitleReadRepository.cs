namespace Request.Data.Repository;

public class RequestTitleReadRepository : BaseReadRepository<RequestTitle, long>, IRequestTitleReadRepository
{
    public RequestTitleReadRepository(RequestDbContext dbContext) : base(dbContext)
    {
    }
}