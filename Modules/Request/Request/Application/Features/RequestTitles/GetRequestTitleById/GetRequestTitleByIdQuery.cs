namespace Request.Application.Features.RequestTitles.GetRequestTitleById;

public record GetRequestTitleByIdQuery(Guid RequestId, Guid Id) : IQuery<GetRequestTitleByIdResult>;
