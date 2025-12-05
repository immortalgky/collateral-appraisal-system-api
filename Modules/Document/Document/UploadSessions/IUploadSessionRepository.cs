using Document.UploadSessions.Model;
using Shared.Data;

namespace Document.UploadSessions;

public interface IUploadSessionRepository : IRepository<UploadSession, Guid>;