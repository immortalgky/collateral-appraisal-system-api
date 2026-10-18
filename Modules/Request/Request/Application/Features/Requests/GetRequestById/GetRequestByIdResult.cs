namespace Request.Application.Features.Requests.GetRequestById;

public sealed record GetRequestByIdResult
{
    public Guid Id { get; init; }
    public string? RequestNumber { get; init; }
    public string Status { get; init; } = default!;
    public string? Purpose { get; init; }
    public string? Channel { get; init; }
    public DateTime? RequestedAt { get; init; }
    public RequestorDetailDto Requestor { get; init; } = default!;
    public UserInfoDto Creator { get; init; } = default!;
    public string? Priority { get; init; }
    public bool IsPma { get; init; }

    /// <summary>
    /// Created by Initiate for a COLLATREV book: its purpose and prior appraisal are locked from the start.
    /// Only the fact is exposed, never the book number or collateral id behind it.
    /// </summary>
    public bool IsPeriodicalReappraisal { get; init; }
    public RequestDetailDto? Detail { get; init; }
    public List<RequestCustomerDto>? Customers { get; init; }
    public List<RequestPropertyDto>? Properties { get; init; }
    public List<RequestTitleDto>? Titles { get; init; }
    public List<RequestDocumentDto>? Documents { get; init; }
};