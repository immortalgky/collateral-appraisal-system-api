using Shared.Identity;

namespace Appraisal.Application.Features.MarketComparables.DeleteMarketComparable;

public class DeleteMarketComparableCommandHandler(
    IMarketComparableRepository marketComparableRepository,
        ICurrentUserService currentUserService
    ) : ICommandHandler<DeleteMarketComparableCommand, DeleteMarketComparableResult>
{
    public async Task<DeleteMarketComparableResult> Handle(DeleteMarketComparableCommand command, CancellationToken cancellationToken)
    {
        var marketComparable = await marketComparableRepository.GetByIdWithDetailsAsync(command.Id, cancellationToken);
        if (marketComparable is null) throw new NotFoundException("MarketComparable Not Found: ", command.Id);

        marketComparable.Delete(currentUserService.UserId ?? null);
        return new DeleteMarketComparableResult(true);
    }
}
