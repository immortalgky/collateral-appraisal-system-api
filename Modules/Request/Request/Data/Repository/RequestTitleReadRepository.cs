namespace Request.Data.Repository;

public class RequestTitleReadRepository : BaseReadRepository<RequestTitle, Guid>, IRequestTitleReadRepository
{
    public RequestTitleReadRepository(RequestDbContext dbContext) : base(dbContext)
    {
    }
}