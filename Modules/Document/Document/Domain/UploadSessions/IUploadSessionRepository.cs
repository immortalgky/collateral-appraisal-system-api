using Document.Domain.UploadSessions.Model;
using Shared.Data;

namespace Document.Domain.UploadSessions;

public interface IUploadSessionRepository : IRepository<UploadSession, Guid>;