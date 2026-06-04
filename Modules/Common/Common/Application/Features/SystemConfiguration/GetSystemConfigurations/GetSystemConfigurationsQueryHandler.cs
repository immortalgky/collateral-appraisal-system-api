using Common.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shared.CQRS;

namespace Common.Application.Features.SystemConfiguration.GetSystemConfigurations;

public class GetSystemConfigurationsQueryHandler(CommonDbContext dbContext)
    : IQueryHandler<GetSystemConfigurationsQuery, List<SystemConfigurationDto>>
{
    public async Task<List<SystemConfigurationDto>> Handle(
        GetSystemConfigurationsQuery query,
        CancellationToken cancellationToken)
    {
        return await dbContext.SystemConfigurations
            .AsNoTracking()
            .OrderBy(c => c.Category)
            .ThenBy(c => c.Key)
            .Select(c => new SystemConfigurationDto(
                c.Id,
                c.Key,
                c.Value,
                c.ValueType,
                c.Description,
                c.Category,
                c.IsActive))
            .ToListAsync(cancellationToken);
    }
}
