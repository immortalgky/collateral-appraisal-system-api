using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Parameter.Addresses.Models;
using Parameter.Data;

namespace Parameter.Addresses.Features.AdminAddresses;

/// <summary>
/// Maintenance for the two Thai address masters, which until now were seed-only (read endpoints
/// <c>/parameters/addresses/title</c> and <c>/parameters/addresses/dopa</c>, no write path at all).
///
/// Title (Department of Lands) and DOPA (civil registration) are SEPARATE datasets that have
/// diverged: Title carries historical and merged provinces (พระนคร, ธนบุรี, กรุงเก่า), non-numeric
/// codes such as "A0"/"A1"/"A2", and nullable postcodes. They therefore share a route shape and a
/// DTO but never share rows — <c>{dataset}</c> selects which pair of tables is touched.
///
/// The flat read endpoints return every sub-district in one payload, which suits dropdowns but not
/// an editor over ~11k rows, so this exposes the hierarchy one level at a time instead.
///
/// Codes are natural keys referenced by child rows and by collateral (which stores the geocode, not
/// the name), so a code is fixed at creation; renaming and reparenting are the supported edits.
/// </summary>
public class AddressAdminEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/parameters/addresses/{dataset}")
            .WithTags("Parameter")
            .RequireAuthorization("address-master.manage");

        group.MapGet("/provinces", GetProvinces);
        group.MapPost("/provinces", CreateProvince);
        group.MapPut("/provinces/{code}", UpdateProvince);
        group.MapDelete("/provinces/{code}", DeleteProvince);

        group.MapGet("/districts", GetDistricts);
        group.MapPost("/districts", CreateDistrict);
        group.MapPut("/districts/{code}", UpdateDistrict);
        group.MapDelete("/districts/{code}", DeleteDistrict);

        group.MapGet("/sub-districts", GetSubDistricts);
        group.MapPost("/sub-districts", CreateSubDistrict);
        group.MapPut("/sub-districts/{code}", UpdateSubDistrict);
        group.MapDelete("/sub-districts/{code}", DeleteSubDistrict);
    }

    // ── Provinces ─────────────────────────────────────────────────────────────

    private static async Task<IResult> GetProvinces(
        string dataset, ParameterDbContext db, CancellationToken ct)
    {
        if (!TryParseDataset(dataset, out var isTitle)) return UnknownDataset(dataset);

        // Child counts drive the delete guard in the UI, so return them with the list rather than
        // making the screen probe each province.
        var rows = isTitle
            ? await db.TitleProvinces.AsNoTracking()
                .Select(p => new ProvinceDto(p.Code, p.NameTh, p.NameEn, p.Districts.Count))
                .ToListAsync(ct)
            : await db.DopaProvinces.AsNoTracking()
                .Select(p => new ProvinceDto(p.Code, p.NameTh, p.NameEn, p.Districts.Count))
                .ToListAsync(ct);

        return Results.Ok(rows.OrderBy(p => p.Code, StringComparer.Ordinal).ToList());
    }

    private static async Task<IResult> CreateProvince(
        string dataset, SaveProvinceRequest request, ParameterDbContext db, CancellationToken ct)
    {
        if (!TryParseDataset(dataset, out var isTitle)) return UnknownDataset(dataset);

        return await Guarded(async () =>
        {
            var code = AddressRules.RequireCode(
                request.Code, nameof(request.Code), AddressRules.ProvinceCodeLength);

            if (await ProvinceExists(db, isTitle, code, ct))
                return Conflict($"Province code '{code}' already exists in the {dataset} dataset.");

            if (isTitle) db.TitleProvinces.Add(TitleProvince.Create(code, request.NameTh, request.NameEn));
            else db.DopaProvinces.Add(DopaProvince.Create(code, request.NameTh, request.NameEn));

            await db.SaveChangesAsync(ct);
            return Results.Created($"/parameters/addresses/{dataset}/provinces/{code}",
                new ProvinceDto(code, request.NameTh.Trim(), request.NameEn.Trim(), 0));
        });
    }

    private static async Task<IResult> UpdateProvince(
        string dataset, string code, SaveProvinceRequest request,
        ParameterDbContext db, CancellationToken ct)
    {
        if (!TryParseDataset(dataset, out var isTitle)) return UnknownDataset(dataset);

        return await Guarded(async () =>
        {
            ProvinceBase? entity = isTitle
                ? await db.TitleProvinces.FirstOrDefaultAsync(p => p.Code == code, ct)
                : await db.DopaProvinces.FirstOrDefaultAsync(p => p.Code == code, ct);
            if (entity is null) return Results.NotFound();

            entity.Rename(request.NameTh, request.NameEn);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new ProvinceDto(entity.Code, entity.NameTh, entity.NameEn, 0));
        });
    }

    private static async Task<IResult> DeleteProvince(
        string dataset, string code, ParameterDbContext db, CancellationToken ct)
    {
        if (!TryParseDataset(dataset, out var isTitle)) return UnknownDataset(dataset);

        // Districts carry ProvinceCode, and collateral rows store the geocode itself, so a province
        // that still has children is never safe to remove.
        var childCount = isTitle
            ? await db.TitleDistricts.CountAsync(d => d.ProvinceCode == code, ct)
            : await db.DopaDistricts.CountAsync(d => d.ProvinceCode == code, ct);
        if (childCount > 0)
            return InUse($"Province '{code}' still has {childCount} district(s); delete or move those first.");

        if (isTitle)
        {
            var entity = await db.TitleProvinces.FirstOrDefaultAsync(p => p.Code == code, ct);
            if (entity is null) return Results.NotFound();
            db.TitleProvinces.Remove(entity);
        }
        else
        {
            var entity = await db.DopaProvinces.FirstOrDefaultAsync(p => p.Code == code, ct);
            if (entity is null) return Results.NotFound();
            db.DopaProvinces.Remove(entity);
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // ── Districts ─────────────────────────────────────────────────────────────

    private static async Task<IResult> GetDistricts(
        string dataset, string? provinceCode, ParameterDbContext db, CancellationToken ct)
    {
        if (!TryParseDataset(dataset, out var isTitle)) return UnknownDataset(dataset);
        if (string.IsNullOrWhiteSpace(provinceCode))
            return BadRequest("provinceCode is required.");

        var rows = isTitle
            ? await db.TitleDistricts.AsNoTracking()
                .Where(d => d.ProvinceCode == provinceCode)
                .Select(d => new DistrictDto(
                    d.Code, d.NameTh, d.NameEn, d.ProvinceCode, d.SubDistricts.Count))
                .ToListAsync(ct)
            : await db.DopaDistricts.AsNoTracking()
                .Where(d => d.ProvinceCode == provinceCode)
                .Select(d => new DistrictDto(
                    d.Code, d.NameTh, d.NameEn, d.ProvinceCode, d.SubDistricts.Count))
                .ToListAsync(ct);

        return Results.Ok(rows.OrderBy(d => d.Code, StringComparer.Ordinal).ToList());
    }

    private static async Task<IResult> CreateDistrict(
        string dataset, SaveDistrictRequest request, ParameterDbContext db, CancellationToken ct)
    {
        if (!TryParseDataset(dataset, out var isTitle)) return UnknownDataset(dataset);

        return await Guarded(async () =>
        {
            var code = AddressRules.RequireCode(
                request.Code, nameof(request.Code), AddressRules.DistrictCodeLength);
            // Normalise the parent code up front so the lookup, the entity and the response all
            // agree — the entity trims internally, so checking the raw value made " 10" miss.
            var provinceCode = AddressRules.RequireCode(
                request.ProvinceCode, nameof(request.ProvinceCode), AddressRules.ProvinceCodeLength);

            if (!await ProvinceExists(db, isTitle, provinceCode, ct))
                return BadRequest($"Province '{provinceCode}' does not exist in the {dataset} dataset.");

            var exists = isTitle
                ? await db.TitleDistricts.AnyAsync(d => d.Code == code, ct)
                : await db.DopaDistricts.AnyAsync(d => d.Code == code, ct);
            if (exists)
                return Conflict($"District code '{code}' already exists in the {dataset} dataset.");

            if (isTitle)
                db.TitleDistricts.Add(TitleDistrict.Create(
                    code, request.NameTh, request.NameEn, provinceCode));
            else
                db.DopaDistricts.Add(DopaDistrict.Create(
                    code, request.NameTh, request.NameEn, provinceCode));

            await db.SaveChangesAsync(ct);
            return Results.Created($"/parameters/addresses/{dataset}/districts/{code}",
                new DistrictDto(code, request.NameTh.Trim(), request.NameEn.Trim(),
                    provinceCode, 0));
        });
    }

    private static async Task<IResult> UpdateDistrict(
        string dataset, string code, SaveDistrictRequest request,
        ParameterDbContext db, CancellationToken ct)
    {
        if (!TryParseDataset(dataset, out var isTitle)) return UnknownDataset(dataset);

        return await Guarded(async () =>
        {
            DistrictBase? entity = isTitle
                ? await db.TitleDistricts.FirstOrDefaultAsync(d => d.Code == code, ct)
                : await db.DopaDistricts.FirstOrDefaultAsync(d => d.Code == code, ct);
            if (entity is null) return Results.NotFound();

            var provinceCode = AddressRules.RequireCode(
                request.ProvinceCode, nameof(request.ProvinceCode), AddressRules.ProvinceCodeLength);

            if (!await ProvinceExists(db, isTitle, provinceCode, ct))
                return BadRequest($"Province '{provinceCode}' does not exist in the {dataset} dataset.");

            entity.Rename(request.NameTh, request.NameEn);
            entity.MoveToProvince(provinceCode);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new DistrictDto(
                entity.Code, entity.NameTh, entity.NameEn, entity.ProvinceCode, 0));
        });
    }

    private static async Task<IResult> DeleteDistrict(
        string dataset, string code, ParameterDbContext db, CancellationToken ct)
    {
        if (!TryParseDataset(dataset, out var isTitle)) return UnknownDataset(dataset);

        var childCount = isTitle
            ? await db.TitleSubDistricts.CountAsync(s => s.DistrictCode == code, ct)
            : await db.DopaSubDistricts.CountAsync(s => s.DistrictCode == code, ct);
        if (childCount > 0)
            return InUse($"District '{code}' still has {childCount} sub-district(s); delete or move those first.");

        if (isTitle)
        {
            var entity = await db.TitleDistricts.FirstOrDefaultAsync(d => d.Code == code, ct);
            if (entity is null) return Results.NotFound();
            db.TitleDistricts.Remove(entity);
        }
        else
        {
            var entity = await db.DopaDistricts.FirstOrDefaultAsync(d => d.Code == code, ct);
            if (entity is null) return Results.NotFound();
            db.DopaDistricts.Remove(entity);
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // ── Sub-districts ─────────────────────────────────────────────────────────

    private static async Task<IResult> GetSubDistricts(
        string dataset, string? districtCode, ParameterDbContext db, CancellationToken ct)
    {
        if (!TryParseDataset(dataset, out var isTitle)) return UnknownDataset(dataset);
        if (string.IsNullOrWhiteSpace(districtCode))
            return BadRequest("districtCode is required.");

        var rows = isTitle
            ? await db.TitleSubDistricts.AsNoTracking()
                .Where(s => s.DistrictCode == districtCode)
                .Select(s => new SubDistrictDto(
                    s.Code, s.NameTh, s.NameEn, s.DistrictCode, s.Postcode))
                .ToListAsync(ct)
            : await db.DopaSubDistricts.AsNoTracking()
                .Where(s => s.DistrictCode == districtCode)
                .Select(s => new SubDistrictDto(
                    s.Code, s.NameTh, s.NameEn, s.DistrictCode, s.Postcode))
                .ToListAsync(ct);

        return Results.Ok(rows.OrderBy(s => s.Code, StringComparer.Ordinal).ToList());
    }

    private static async Task<IResult> CreateSubDistrict(
        string dataset, SaveSubDistrictRequest request, ParameterDbContext db, CancellationToken ct)
    {
        if (!TryParseDataset(dataset, out var isTitle)) return UnknownDataset(dataset);

        return await Guarded(async () =>
        {
            var code = AddressRules.RequireCode(
                request.Code, nameof(request.Code), AddressRules.SubDistrictCodeLength);
            var districtCode = AddressRules.RequireCode(
                request.DistrictCode, nameof(request.DistrictCode), AddressRules.DistrictCodeLength);

            if (!await DistrictExists(db, isTitle, districtCode, ct))
                return BadRequest($"District '{districtCode}' does not exist in the {dataset} dataset.");

            var exists = isTitle
                ? await db.TitleSubDistricts.AnyAsync(s => s.Code == code, ct)
                : await db.DopaSubDistricts.AnyAsync(s => s.Code == code, ct);
            if (exists)
                return Conflict($"Sub-district code '{code}' already exists in the {dataset} dataset.");

            if (isTitle)
                db.TitleSubDistricts.Add(TitleSubDistrict.Create(
                    code, request.NameTh, request.NameEn, districtCode, request.Postcode));
            else
                db.DopaSubDistricts.Add(DopaSubDistrict.Create(
                    code, request.NameTh, request.NameEn, districtCode, request.Postcode));

            await db.SaveChangesAsync(ct);
            return Results.Created($"/parameters/addresses/{dataset}/sub-districts/{code}",
                new SubDistrictDto(code, request.NameTh.Trim(), request.NameEn.Trim(),
                    districtCode, AddressRules.NormalisePostcode(request.Postcode)));
        });
    }

    private static async Task<IResult> UpdateSubDistrict(
        string dataset, string code, SaveSubDistrictRequest request,
        ParameterDbContext db, CancellationToken ct)
    {
        if (!TryParseDataset(dataset, out var isTitle)) return UnknownDataset(dataset);

        return await Guarded(async () =>
        {
            SubDistrictBase? entity = isTitle
                ? await db.TitleSubDistricts.FirstOrDefaultAsync(s => s.Code == code, ct)
                : await db.DopaSubDistricts.FirstOrDefaultAsync(s => s.Code == code, ct);
            if (entity is null) return Results.NotFound();

            var districtCode = AddressRules.RequireCode(
                request.DistrictCode, nameof(request.DistrictCode), AddressRules.DistrictCodeLength);

            if (!await DistrictExists(db, isTitle, districtCode, ct))
                return BadRequest($"District '{districtCode}' does not exist in the {dataset} dataset.");

            entity.Update(request.NameTh, request.NameEn, request.Postcode);
            entity.MoveToDistrict(districtCode);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new SubDistrictDto(
                entity.Code, entity.NameTh, entity.NameEn, entity.DistrictCode, entity.Postcode));
        });
    }

    private static async Task<IResult> DeleteSubDistrict(
        string dataset, string code, ParameterDbContext db, CancellationToken ct)
    {
        if (!TryParseDataset(dataset, out var isTitle)) return UnknownDataset(dataset);

        // A sub-district geocode is stored on collateral/request rows in OTHER schemas, which this
        // module cannot query. Deleting one that is in use would orphan those addresses, so the
        // screen warns and this stays a deliberate, low-frequency action.
        if (isTitle)
        {
            var entity = await db.TitleSubDistricts.FirstOrDefaultAsync(s => s.Code == code, ct);
            if (entity is null) return Results.NotFound();
            db.TitleSubDistricts.Remove(entity);
        }
        else
        {
            var entity = await db.DopaSubDistricts.FirstOrDefaultAsync(s => s.Code == code, ct);
            if (entity is null) return Results.NotFound();
            db.DopaSubDistricts.Remove(entity);
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool TryParseDataset(string dataset, out bool isTitle)
    {
        isTitle = string.Equals(dataset, "title", StringComparison.OrdinalIgnoreCase);
        return isTitle || string.Equals(dataset, "dopa", StringComparison.OrdinalIgnoreCase);
    }

    private static Task<bool> ProvinceExists(
        ParameterDbContext db, bool isTitle, string code, CancellationToken ct) =>
        isTitle
            ? db.TitleProvinces.AnyAsync(p => p.Code == code, ct)
            : db.DopaProvinces.AnyAsync(p => p.Code == code, ct);

    private static Task<bool> DistrictExists(
        ParameterDbContext db, bool isTitle, string code, CancellationToken ct) =>
        isTitle
            ? db.TitleDistricts.AnyAsync(d => d.Code == code, ct)
            : db.DopaDistricts.AnyAsync(d => d.Code == code, ct);

    /// <summary>
    /// Domain validation throws ArgumentException; surface it as a 400 rather than a 500.
    ///
    /// Also maps a unique-constraint violation to the same 409 the duplicate pre-check returns.
    /// The pre-check and SaveChanges are not atomic, so two concurrent creates of the same code
    /// both pass the check and one hits the primary key — the PK is the real guarantee, the
    /// pre-check only makes the common case a friendlier message.
    /// </summary>
    private static async Task<IResult> Guarded(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Conflict("That code already exists in this dataset.");
        }
    }

    // 2627 = PK / unique constraint, 2601 = unique index. Anything else is a genuine failure and
    // is left to propagate.
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2627 or 2601 };

    private static IResult UnknownDataset(string dataset) =>
        BadRequest($"Unknown address dataset '{dataset}'. Expected 'title' or 'dopa'.");

    private static IResult BadRequest(string detail) =>
        Results.Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);

    private static IResult Conflict(string detail) =>
        Results.Problem(detail: detail, statusCode: StatusCodes.Status409Conflict);

    private static IResult InUse(string detail) =>
        Results.Problem(detail: detail, statusCode: StatusCodes.Status409Conflict);
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record ProvinceDto(string Code, string NameTh, string NameEn, int DistrictCount);

public record DistrictDto(
    string Code, string NameTh, string NameEn, string ProvinceCode, int SubDistrictCount);

public record SubDistrictDto(
    string Code, string NameTh, string NameEn, string DistrictCode, string? Postcode);

public record SaveProvinceRequest(string Code, string NameTh, string NameEn);

public record SaveDistrictRequest(string Code, string NameTh, string NameEn, string ProvinceCode);

public record SaveSubDistrictRequest(
    string Code, string NameTh, string NameEn, string DistrictCode, string? Postcode);
