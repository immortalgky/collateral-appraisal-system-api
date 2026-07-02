namespace Auth.Application.Features.Requestors.SearchRequestors;

public record RequestorItemDto(
    string EmployeeId,
    string Name,
    string? Email,
    string? ContactNo,
    string? AoCode,
    string? CostCenterCode,
    string? CostCenterDescription,
    string? Department);

public record SearchRequestorsResult(IEnumerable<RequestorItemDto> Items, long Count, int PageNumber, int PageSize);
