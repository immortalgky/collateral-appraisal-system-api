namespace Parameter.Addresses.Features.GetDopaAddresses;

internal class GetDopaAddressesQueryHandler(IAddressRepository addressRepository)
    : IQueryHandler<GetDopaAddressesQuery, GetAddressesResult>
{
    public async Task<GetAddressesResult> Handle(GetDopaAddressesQuery query, CancellationToken cancellationToken)
    {
        var addresses = await addressRepository.GetDopaAddressesAsync(cancellationToken);

        return new GetAddressesResult(addresses);
    }
}
