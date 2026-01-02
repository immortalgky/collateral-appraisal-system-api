namespace Request.Application.Features.Requests.GetRequestById;

public record GetRequestByIdResult
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; }
    public string Status { get; set; }
    public string Purpose { get; set; }
    public string Channel { get; set; }
    public UserInfoDto Requestor { get; set; }
    public UserInfoDto Creator { get; set; }
    public string Priority { get; set; }
    public bool IsPma { get; set; }
    public RequestDetailDto Detail { get; set; }
    public List<RequestCustomerDto> Customers { get; set; }
    public List<RequestPropertyDto> Properties { get; set; }
    public List<RequestTitleDto> Titles { get; set; }
    public List<RequestDocumentDto> Documents { get; set; }
};