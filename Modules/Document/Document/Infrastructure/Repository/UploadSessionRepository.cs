using Document.Domain.UploadSessions;
using Document.Domain.UploadSessions.Model;
using Shared.Data;

namespace Document.Data.Repository;

public class UploadSessionRepository(DocumentDbContext dbContext)
    : BaseRepository<UploadSession, Guid>(dbContext), IUploadSessionRepository;