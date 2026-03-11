namespace Parameter.Data.Repository;

public interface IAddressRepository
{
    Task<List<AddressDto>> GetTitleAddressesAsync(CancellationToken ct = default);
    Task<List<AddressDto>> GetDopaAddressesAsync(CancellationToken ct = default);
}
