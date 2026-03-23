using Parameter.ConstructionWork.Models;

namespace Parameter.ConstructionWork.Features.GetConstructionWorkGroups;

public class GetConstructionWorkGroupsQueryHandler(
    ParameterDbContext context
) : IQueryHandler<GetConstructionWorkGroupsQuery, GetConstructionWorkGroupsResult>
{
    public async Task<GetConstructionWorkGroupsResult> Handle(
        GetConstructionWorkGroupsQuery query,
        CancellationToken cancellationToken)
    {
        var groups = await context.ConstructionWorkGroups
            .Where(g => g.IsActive)
            .Include(g => g.WorkItems.Where(i => i.IsActive).OrderBy(i => i.DisplayOrder))
            .OrderBy(g => g.DisplayOrder)
            .ToListAsync(cancellationToken);

        var dtos = groups.Select(g => new ConstructionWorkGroupDto(
            g.Id, g.Code, g.NameTh, g.NameEn, g.DisplayOrder,
            g.WorkItems.Select(i => new ConstructionWorkItemDto(
                i.Id, i.Code, i.NameTh, i.NameEn, i.DisplayOrder
            )).ToList()
        )).ToList();

        return new GetConstructionWorkGroupsResult(dtos);
    }
}
