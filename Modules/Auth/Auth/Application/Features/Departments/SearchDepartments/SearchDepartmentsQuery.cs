namespace Auth.Application.Features.Departments.SearchDepartments;

public record SearchDepartmentsQuery(
    string? Search,
    int PageSize = 50)
    : IQuery<SearchDepartmentsResult>;
