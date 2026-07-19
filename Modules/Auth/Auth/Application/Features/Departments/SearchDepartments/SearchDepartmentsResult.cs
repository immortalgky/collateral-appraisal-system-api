namespace Auth.Application.Features.Departments.SearchDepartments;

public record DepartmentItemDto(string Code, string? Description);

public record SearchDepartmentsResult(IEnumerable<DepartmentItemDto> Items);
