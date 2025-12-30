namespace Request.Application.Features.Requests.GetRequestById;

public record GetRequestByIdQuery(Guid Id) : IQuery<GetRequestByIdResult>;