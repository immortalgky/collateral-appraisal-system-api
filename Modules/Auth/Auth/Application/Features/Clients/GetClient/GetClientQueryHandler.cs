using OpenIddict.Abstractions;
using Shared.CQRS;
using Shared.Exceptions;

namespace Auth.Application.Features.Clients.GetClient;

public record GetClientQuery(string Id) : IQuery<ClientDetailDto>;

public class GetClientQueryHandler(IOpenIddictApplicationManager applicationManager)
    : IQueryHandler<GetClientQuery, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(GetClientQuery request, CancellationToken cancellationToken)
    {
        var app = await applicationManager.FindByIdAsync(request.Id, cancellationToken)
                  ?? throw new NotFoundException("Client", request.Id);

        return await ClientPermissionMapper.ToDetailDtoAsync(applicationManager, app, cancellationToken);
    }
}
