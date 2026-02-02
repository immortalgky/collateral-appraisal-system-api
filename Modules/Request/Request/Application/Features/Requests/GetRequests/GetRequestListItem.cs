namespace Request.Application.Features.Requests.GetRequests;

public sealed record GetRequestListItem
{
    public Guid Id { get; init; }
    public string RequestNumber { get; init; } = "";
    public string Status { get; init; } = "";
    public string? Purpose { get; init; }
    public string? Channel { get; init; }
    public string? Priority { get; init; }
}
