namespace Request.Requests.Features.SubmittedRequest;

public record SubmittedRequestQuery(long RequestId) : IQuery<SubmittedRequestResult>;