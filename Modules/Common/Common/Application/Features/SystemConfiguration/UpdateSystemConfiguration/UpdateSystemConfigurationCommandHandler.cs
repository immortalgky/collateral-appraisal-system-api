using Common.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Shared.CQRS;
using Shared.Exceptions;

namespace Common.Application.Features.SystemConfiguration.UpdateSystemConfiguration;

public class UpdateSystemConfigurationCommandHandler(
    CommonDbContext dbContext,
    IMemoryCache cache)
    : ICommandHandler<UpdateSystemConfigurationCommand, Unit>
{
    public async Task<Unit> Handle(
        UpdateSystemConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        var config = await dbContext.SystemConfigurations
            .FirstOrDefaultAsync(c => c.Key == command.Key, cancellationToken)
            ?? throw new NotFoundException($"SystemConfiguration with key '{command.Key}' was not found.");

        config.UpdateValue(command.Value);

        if (command.Description is not null)
            config.UpdateDescription(command.Description);

        if (command.IsActive.HasValue)
            config.SetActive(command.IsActive.Value);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate the reader cache for this key so next read gets fresh data
        cache.Remove($"syscfg:{command.Key}");

        return Unit.Value;
    }
}
