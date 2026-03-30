using Auth.Domain.Groups;
using Shared.Pagination;

namespace Auth.Application.Services;

public interface IGroupService
{
    Task<Group> CreateGroup(string name, string description, string scope, Guid? companyId, CancellationToken cancellationToken = default);
    Task<PaginatedResult<Group>> GetGroups(string? search, string? scope, PaginationRequest paginationRequest, CancellationToken cancellationToken = default);
    Task<Group?> GetGroupById(Guid id, CancellationToken cancellationToken = default);
    Task UpdateGroup(Guid id, string name, string description, CancellationToken cancellationToken = default);
    Task UpdateGroupUsers(Guid id, List<Guid> userIds, CancellationToken cancellationToken = default);
    Task UpdateGroupMonitoring(Guid id, List<Guid> monitoredGroupIds, CancellationToken cancellationToken = default);
    Task DeleteGroup(Guid id, Guid? deletedBy, CancellationToken cancellationToken = default);
}
