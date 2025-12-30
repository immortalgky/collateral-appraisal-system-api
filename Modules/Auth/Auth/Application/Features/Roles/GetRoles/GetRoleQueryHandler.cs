using Auth.Services;

namespace Auth.Domain.Roles.Features.GetRoles;

public class GetRoleQueryHandler(IRoleService roleService)
    : IQueryHandler<GetRoleQuery, GetRoleResult>
{
    public async Task<GetRoleResult> Handle(GetRoleQuery query, CancellationToken cancellationToken)
    {
        var pagination = await roleService.GetRoles(query.PaginationRequest, cancellationToken);
        var paginationDto = new PaginatedResult<RoleDto>(
            pagination.Items.Select(item => item.ToDto()),
            pagination.Count,
            pagination.PageNumber,
            pagination.PageSize
        );

        return new GetRoleResult(paginationDto);
    }
}
