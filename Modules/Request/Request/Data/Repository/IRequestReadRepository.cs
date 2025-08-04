using Shared.Data;
using Shared.DDD;

namespace Request.Data.Repository;

public interface IRequestReadRepository : IReadRepository<Requests.Models.Request, long>
{
}