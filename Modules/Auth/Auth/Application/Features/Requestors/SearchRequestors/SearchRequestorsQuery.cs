namespace Auth.Application.Features.Requestors.SearchRequestors;

public record SearchRequestorsQuery(
    string? Search,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<SearchRequestorsResult>;
