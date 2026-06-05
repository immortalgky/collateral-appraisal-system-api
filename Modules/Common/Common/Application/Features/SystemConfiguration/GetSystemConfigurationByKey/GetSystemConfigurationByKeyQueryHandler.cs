using Common.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.CQRS;

namespace Common.Application.Features.SystemConfiguration.GetSystemConfigurationByKey;

public class GetSystemConfigurationByKeyQueryHandler(CommonDbContext dbContext)
    : IQueryHandler<GetSystemConfigurationByKeyQuery, SystemConfigurationDto?>
{
    public async Task<SystemConfigurationDto?> Handle(
        GetSystemConfigurationByKeyQuery query,
        CancellationToken cancellationToken)
    {
        return await dbContext.SystemConfigurations
            .AsNoTracking()
            .Where(c => c.Key == query.Key)
            .Select(c => new SystemConfigurationDto(
                c.Id,
                c.Key,
                c.Value,
                c.ValueType,
                c.Description,
                c.Category,
                c.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
