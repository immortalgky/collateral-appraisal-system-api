using Document.UploadSessions;
using Document.UploadSessions.Model;
using Shared.Data;

namespace Document.Data.Repository;

public class UploadSessionRepository(DocumentDbContext dbContext)
    : BaseRepository<UploadSession, Guid>(dbContext), IUploadSessionRepository;