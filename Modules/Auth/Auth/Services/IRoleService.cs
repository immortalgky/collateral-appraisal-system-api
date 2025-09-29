using OAuth2OpenId.Identity.Models;

namespace Auth.Services;

public interface IRoleService
{
    public Task<ApplicationRole> CreateRole(
        CreateRoleDto roleDto,
        CancellationToken cancellationToken = default
    );
    public Task<PaginatedResult<ApplicationRole>> GetRoles(
        PaginationRequest paginationRequest,
        CancellationToken cancellationToken = default
    );
    public Task<ApplicationRole?> GetRoleById(
        Guid id,
        CancellationToken cancellationToken = default
    );
    public Task DeleteRole(Guid id, CancellationToken cancellationToken = default);
}
