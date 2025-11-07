namespace Request.Data.Repository;

public class RequestCommentReadRepository : BaseReadRepository<RequestComment, Guid>, IRequestCommentReadRepository
{
    public RequestCommentReadRepository(RequestDbContext dbContext) : base(dbContext)
    {
    }
}