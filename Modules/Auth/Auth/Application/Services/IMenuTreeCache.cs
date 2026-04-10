using Auth.Domain.Menu;

namespace Auth.Application.Services;

public interface IMenuTreeCache
{
    Task<IReadOnlyList<MenuItem>> GetAllAsync(CancellationToken cancellationToken = default);
    void Invalidate();
}
