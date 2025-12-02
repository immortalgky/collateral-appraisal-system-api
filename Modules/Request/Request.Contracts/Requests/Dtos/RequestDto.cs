using Request.Contracts.RequestDocuments.Dto;

namespace Request.Contracts.Requests.Dtos;

// public record RequestDto(
//     long Id,
//     string AppraisalNo,
//     string Status /*,
//     RequestDetailDto Detail*/
//     , List<RequestCustomerDto> Customers
// );

public class RequestDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string AppraisalNo { get; set; }
    public string Purpose { get; set; }
    public SourceSystemDto SourceSystem { get; set; }
    public string Priority { get; set; }
    public string Status { get; set; }
    public bool IsPMA { get; set; }
    public RequestDetailDto Detail { get; set; }
    public List<RequestCustomerDto> Customers { get; set; }
    public List<RequestPropertyDto> Properties { get; set; }
    public List<RequestDocumentDto> Documents { get; set; }
    public List<RequestTitleDto> Titles { get; set; } = new List<RequestTitleDto>();
}