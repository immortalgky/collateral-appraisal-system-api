namespace Auth.Application.Features.Groups.GetGroupById;

public record GetGroupByIdQuery(Guid Id) : IQuery<GetGroupByIdResult>;
