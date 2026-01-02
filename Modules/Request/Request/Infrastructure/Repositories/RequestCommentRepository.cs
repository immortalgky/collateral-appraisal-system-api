namespace Request.Infrastructure.Repositories;

public class RequestCommentRepository(RequestDbContext dbContext)
    : BaseRepository<RequestComment, Guid>(dbContext), IRequestCommentRepository;