namespace Parameter.Data.Repository;

public class AddressRepository(ParameterDbContext dbContext) : IAddressRepository
{
    public async Task<List<AddressDto>> GetTitleAddressesAsync(CancellationToken ct = default)
    {
        return await dbContext.TitleSubDistricts
            .AsNoTracking()
            .Include(s => s.District)
                .ThenInclude(d => d.Province)
            .OrderBy(s => s.Code)
            .Select(s => new AddressDto(
                s.District.ProvinceCode,
                s.District.Province.NameTh,
                s.District.Province.NameEn,
                s.DistrictCode,
                s.District.NameTh,
                s.District.NameEn,
                s.Code,
                s.NameTh,
                s.NameEn,
                s.Postcode))
            .ToListAsync(ct);
    }

    public async Task<List<AddressDto>> GetDopaAddressesAsync(CancellationToken ct = default)
    {
        return await dbContext.DopaSubDistricts
            .AsNoTracking()
            .Include(s => s.District)
                .ThenInclude(d => d.Province)
            .OrderBy(s => s.Code)
            .Select(s => new AddressDto(
                s.District.ProvinceCode,
                s.District.Province.NameTh,
                s.District.Province.NameEn,
                s.DistrictCode,
                s.District.NameTh,
                s.District.NameEn,
                s.Code,
                s.NameTh,
                s.NameEn,
                s.Postcode))
            .ToListAsync(ct);
    }
}
