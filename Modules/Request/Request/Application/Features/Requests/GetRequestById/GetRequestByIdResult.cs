namespace Request.Application.Features.Requests.GetRequestById;

public sealed record GetRequestByIdResult
{
    public Guid Id { get; init; }
    public string? RequestNumber { get; init; }
    public string Status { get; init; } = default!;
    public string? Purpose { get; init; }
    public string? Channel { get; init; }
    public UserInfoDto Requestor { get; init; } = default!;
    public UserInfoDto Creator { get; init; } = default!;
    public string? Priority { get; init; }
    public bool IsPma { get; init; }
    public RequestDetailDto? Detail { get; init; }
    public List<RequestCustomerDto>? Customers { get; init; }
    public List<RequestPropertyDto>? Properties { get; init; }
    public List<RequestTitleDto>? Titles { get; init; }
    public List<RequestDocumentDto>? Documents { get; init; }
};