namespace Request.Data.Repository;

public class RequestCommentReadRepository : BaseReadRepository<RequestComment, long>, IRequestCommentReadRepository
{
    public RequestCommentReadRepository(RequestDbContext dbContext) : base(dbContext)
    {
    }
}