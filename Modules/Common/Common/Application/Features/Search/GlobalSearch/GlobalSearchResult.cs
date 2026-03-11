namespace Common.Application.Features.Search.GlobalSearch;

public record GlobalSearchResult(GlobalSearchResults Results, int TotalCount);

public record GlobalSearchResults(
    List<SearchResultItem> Requests,
    List<SearchResultItem> Customers,
    List<SearchResultItem> Properties);

public record SearchResultItem(
    string Id,
    string Title,
    string? Subtitle,
    string? Status,
    string Category,
    string NavigateTo,
    string? Icon,
    Dictionary<string, object?> Metadata);
