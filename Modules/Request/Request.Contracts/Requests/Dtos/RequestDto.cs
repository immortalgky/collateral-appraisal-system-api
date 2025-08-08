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
    public long Id { get; set; }
    public string AppraisalNo { get; set; }
    public string Status { get; set; }
    public RequestDetailDto Detail { get; set; }
    public List<RequestCustomerDto> Customers { get; set; }
    public List<RequestPropertyDto> Properties { get; set; }
}