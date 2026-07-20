namespace Auth.Application.Features.Departments.SearchDepartments;

/// <summary>
/// Lookup for the AS400-sourced department reference data, used to populate the department
/// filter on the RCAS reports (RCAS001 Requestor Department, RCAS010/011 Department Code).
/// Those filters bind the raw department CODE, so the caller needs the code list — a free-text
/// input would only ever match if the user happened to type the exact code.
/// </summary>
internal class SearchDepartmentsQueryHandler(AuthDbContext dbContext)
    : IQueryHandler<SearchDepartmentsQuery, SearchDepartmentsResult>
{
    public async Task<SearchDepartmentsResult> Handle(
        SearchDepartmentsQuery query, CancellationToken cancellationToken)
    {
        var q = dbContext.Departments.Where(d => d.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(d =>
                d.Code.Contains(query.Search) ||
                (d.Description != null && d.Description.Contains(query.Search)));

        var items = await q
            .OrderBy(d => d.Code)
            .Take(query.PageSize)
            .Select(d => new DepartmentItemDto(d.Code, d.Description))
            .ToListAsync(cancellationToken);

        return new SearchDepartmentsResult(items);
    }
}
