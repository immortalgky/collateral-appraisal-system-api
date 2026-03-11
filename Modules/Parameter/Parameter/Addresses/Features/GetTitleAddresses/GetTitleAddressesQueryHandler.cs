namespace Parameter.Addresses.Features.GetTitleAddresses;

internal class GetTitleAddressesQueryHandler(IAddressRepository addressRepository)
    : IQueryHandler<GetTitleAddressesQuery, GetAddressesResult>
{
    public async Task<GetAddressesResult> Handle(GetTitleAddressesQuery query, CancellationToken cancellationToken)
    {
        var addresses = await addressRepository.GetTitleAddressesAsync(cancellationToken);

        return new GetAddressesResult(addresses);
    }
}
