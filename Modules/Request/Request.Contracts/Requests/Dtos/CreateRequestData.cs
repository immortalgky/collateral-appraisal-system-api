using Request.Contracts.RequestDocuments.Dto;

namespace Request.Contracts.Requests.Dtos;

public record CreateRequestData(
    string? Purpose,
    string? Channel,
    UserInfoDto Creator,
    string? Priority,
    bool IsPma,
    RequestDetailDto? Detail,
    List<RequestCustomerDto>? Customers,
    List<RequestPropertyDto>? Properties,
    List<RequestTitleDto>? Titles,
    List<RequestDocumentDto>? Documents,
    List<RequestCommentDto>? Comments,
    /// <summary>
    /// UI path: employee code (bank code, e.g. P5229) sent by the requestor picker. The service
    /// resolves the full profile via <see cref="IUserLookupService"/> and stores an org-data
    /// snapshot on the request. Takes precedence over <see cref="Requestor"/>.
    /// </summary>
    string? RequestorEmployeeId = null,
    /// <summary>
    /// Integration / reappraisal path: pre-resolved identity passed directly when
    /// <see cref="RequestorEmployeeId"/> is null. Ignored when <see cref="RequestorEmployeeId"/> is set.
    /// </summary>
    UserInfoDto? Requestor = null
);
