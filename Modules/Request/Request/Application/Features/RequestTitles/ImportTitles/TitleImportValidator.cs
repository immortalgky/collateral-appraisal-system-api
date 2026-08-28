using System.Globalization;
using Parameter.Contracts.Parameters;
using Request.Application.Features.RequestTitles.ImportTitles.Reading;

// AddressDto exists in both Request.Contracts (the persisted shape, geocodes only) and
// Parameter.Contracts (the master row, codes AND names). Both are needed here, so both are named.
using ParameterAddressDto = Parameter.Contracts.Parameters.Dtos.AddressDto;
using ParameterDto = Parameter.Contracts.Parameters.Dtos.ParameterDto;

namespace Request.Application.Features.RequestTitles.ImportTitles;

/// <summary>
/// Turns <see cref="RawSheet"/>s into request titles, collecting a message per problem instead of
/// stopping at the first one.
///
/// The rules here deliberately mirror the domain's own Validate() methods — TitleLand.Validate,
/// TitleDeedInfo.Validate, CondoInfo.Validate, VehicleInfo.Validate, Address.Validate and friends,
/// all of which CreateRequestCommandHandler runs over every title on save. A row that this class
/// accepts must therefore survive the save; anything looser would import cleanly and then blow up
/// later, which is the one failure mode a preview screen exists to prevent.
/// </summary>
public class TitleImportValidator(
    IParameterLookupService parameterLookup,
    IAddressLookupService addressLookup)
{
    /// <summary>Deed types accepted by TitleDeedInfo.Validate — kept in sync with that array.</summary>
    private static readonly string[] ValidDeedTypes = ["DEED", "NS3", "NS3K", "NS3KO", "POSR", "OTHER"];

    // Cell VALUES, not messages: users type these into the spreadsheet, and the Thai forms are the
    // ones the on-screen toggle shows, so a file transcribed from the UI still reads correctly.
    private static readonly string[] TruthyValues = ["true", "1", "y", "yes", "จดทะเบียนแล้ว", "ใช่", "มี"];
    private static readonly string[] FalsyValues = ["false", "0", "n", "no", "ไม่ได้จดทะเบียน", "ไม่ใช่", "ไม่มี", "-"];

    public async Task<TitleImportResult> ValidateAsync(
        IReadOnlyList<RawSheet> sheets,
        CancellationToken cancellationToken)
    {
        var validCodes = await LoadParameterCodesAsync(cancellationToken);

        var rows = new List<TitleImportRow>();
        var errors = new List<TitleImportRowError>();
        var ignoredSheets = new List<string>();
        var totalRows = 0;
        var recognisedSheets = 0;

        // The cap counts what the user actually typed. Counting every worksheet instead would spend
        // it on the template's own Reference/Instructions/Lists sheets — ~270 rows of the 500 before
        // the file holds a single title.
        var dataSheets = sheets
            .Where(s => !TitleImportCatalog.IsTemplateHelperSheet(s.Name)
                        && TitleImportCatalog.FindSheet(s.Name) is not null)
            .ToList();

        // Count non-blank rows among what was read, but fall back to the sheet's true size when the
        // reader stopped early — otherwise a 50k-row file would be reported as holding 501.
        TitleImportLimits.GuardRowCount(
            dataSheets.Sum(s => s.Rows.Count < s.TotalDataRows
                ? s.TotalDataRows
                : s.Rows.Count(cells => cells.Any(c => !string.IsNullOrWhiteSpace(c)))));

        foreach (var sheet in sheets)
        {
            // The template's own Reference and Instructions sheets are ours, not the user's — never
            // report them as something the importer failed to understand.
            if (TitleImportCatalog.IsTemplateHelperSheet(sheet.Name)) continue;

            var definition = TitleImportCatalog.FindSheet(sheet.Name);
            if (definition is null)
            {
                ignoredSheets.Add(sheet.Name);
                continue;
            }

            recognisedSheets++;

            var headerMap = BuildHeaderMap(sheet, definition);

            for (var i = 0; i < sheet.Rows.Count; i++)
            {
                var cells = sheet.Rows[i];
                if (cells.All(string.IsNullOrWhiteSpace)) continue;

                totalRows++;
                var rowNumber = sheet.RowNumberOf(i);
                var context = new RowContext(definition, headerMap, cells, sheet.Name, rowNumber, errors);

                var row = await BuildRowAsync(context, validCodes, cancellationToken);
                if (row is not null) rows.Add(row);
            }
        }

        // Two different mistakes, two different fixes — telling an empty template "no recognised
        // worksheet" sends the user off to re-download the file they are already holding.
        if (recognisedSheets == 0)
            throw new BadRequestException(
                "This file contains no recognised worksheet. Download the template and fill in the " +
                "sheets it provides" +
                (ignoredSheets.Count == 0
                    ? "."
                    : $". Sheets found: {string.Join(", ", ignoredSheets)}."));

        if (totalRows == 0)
            throw new BadRequestException(
                "No data rows were found. Enter the titles from row 2 onwards in the sheet that matches " +
                $"the collateral type ({string.Join(", ", TitleImportCatalog.Sheets.Select(s => s.Key))}), then upload again.");

        return new TitleImportResult(totalRows, rows, errors, ignoredSheets);
    }

    // ── Row assembly ─────────────────────────────────────────────────────────

    private async Task<TitleImportRow?> BuildRowAsync(
        RowContext ctx,
        IReadOnlyDictionary<string, IReadOnlySet<string>> validCodes,
        CancellationToken cancellationToken)
    {
        var errorsBefore = ctx.Errors.Count;

        // ── Collateral type: decides which of the rules below even apply ─────
        var collateralTypeCode = TitleImportCatalog.StripCodeLabel(ctx.Text("collateralType"));
        CollateralType? collateralType = null;

        if (string.IsNullOrWhiteSpace(collateralTypeCode))
        {
            ctx.Error("collateralType", "Collateral type is required.");
        }
        else if (!CollateralType.TryFromCode(collateralTypeCode, out collateralType))
        {
            ctx.Error("collateralType",
                $"'{collateralTypeCode}' is not a known collateral type code. See the Reference sheet for the valid list.");
        }
        else if (!ctx.Sheet.Families.Contains(collateralType!.FamilyCode))
        {
            var belongsTo = TitleImportCatalog.Sheets.FirstOrDefault(s => s.Families.Contains(collateralType.FamilyCode));
            ctx.Error("collateralType",
                $"Code '{collateralTypeCode}' ({collateralType.DisplayName}) does not belong in the {ctx.Sheet.Key} sheet" +
                (belongsTo is null ? "." : $" — move this row to the {belongsTo.Key} sheet ({belongsTo.Name})."));
            collateralType = null;
        }

        var family = collateralType?.FamilyCode;

        // ── Per-column parsing: type, length and range ───────────────────────
        var values = new Dictionary<string, object?>();
        foreach (var column in ctx.Sheet.Columns)
            values[column.Key] = ctx.Parse(column, validCodes);

        // ── Required-ness, mirroring the domain ─────────────────────────────
        if (family is not null) ValidateRequiredForFamily(ctx, family, values);

        // ── Addresses: names in, geocodes out ───────────────────────────────
        var titleAddress = await ResolveAddressAsync(ctx, "titleAddress", isDopa: false, cancellationToken);
        var dopaAddress = await ResolveAddressAsync(ctx, "dopaAddress", isDopa: true, cancellationToken);

        if (ctx.Errors.Count > errorsBefore || collateralType is null) return null;

        var dto = new RequestTitleDto
        {
            CollateralType = collateralTypeCode!,
            CollateralStatus = values.GetValueOrDefault("collateralStatus") as bool? ?? false,

            TitleNumber = Str(values, "titleNumber"),
            TitleType = Str(values, "titleType"),
            TitleDetail = null,

            BookNumber = Str(values, "bookNumber"),
            PageNumber = Str(values, "pageNumber"),
            LandParcelNumber = Str(values, "landParcelNumber"),
            SurveyNumber = Str(values, "surveyNumber"),
            MapSheetNumber = Str(values, "mapSheetNumber"),
            Rawang = Str(values, "rawang"),
            AerialMapName = Str(values, "aerialMapName"),
            AerialMapNumber = Str(values, "aerialMapNumber"),

            AreaRai = Int(values, "areaRai"),
            AreaNgan = Int(values, "areaNgan"),
            AreaSquareWa = Dec(values, "areaSquareWa"),

            OwnerName = Str(values, "ownerName"),

            VehicleType = Str(values, "vehicleType"),
            VehicleLocation = Str(values, "vehicleLocation"),
            VIN = Str(values, "vin"),
            LicensePlateNumber = Str(values, "licensePlateNumber"),

            VesselType = Str(values, "vesselType"),
            VesselLocation = Str(values, "vesselLocation"),
            HIN = Str(values, "hin"),
            VesselRegistrationNumber = Str(values, "vesselRegistrationNumber"),

            RegistrationStatus = values.GetValueOrDefault("registrationStatus") as bool? ?? false,
            RegistrationNumber = Str(values, "registrationNumber"),
            MachineType = Str(values, "machineType"),
            InstallationStatus = Str(values, "installationStatus"),
            InvoiceNumber = Str(values, "invoiceNumber"),
            NumberOfMachine = Int(values, "numberOfMachine"),

            BuildingType = Str(values, "buildingType"),
            UsableArea = Dec(values, "usableArea"),
            NumberOfBuilding = Int(values, "numberOfBuilding"),

            CondoName = Str(values, "condoName"),
            BuildingNumber = Str(values, "buildingNumber"),
            CondoRegistrationNumber = Str(values, "condoRegistrationNumber"),
            RoomNumber = Str(values, "roomNumber"),
            FloorNumber = Str(values, "floorNumber"),

            TitleAddress = titleAddress.Dto,
            DopaAddress = dopaAddress.Dto,

            Notes = Str(values, "notes"),
            Documents = []
        };

        return new TitleImportRow(
            ctx.SheetName, ctx.RowNumber, dto,
            titleAddress.SubDistrictName, titleAddress.DistrictName, titleAddress.ProvinceName,
            dopaAddress.SubDistrictName, dopaAddress.DistrictName, dopaAddress.ProvinceName);
    }

    /// <summary>
    /// Which fields a row must carry, per collateral family.
    ///
    /// Two rule sets have to hold for an imported row to be usable, and this method is the union of
    /// both: the domain's Title*.Validate() overrides, which the save path runs, and the on-screen
    /// form's own schema, built from the field configs in the frontend. Requiring only the domain's
    /// share lets a row import and then sit in the list marked incomplete; requiring only the form's
    /// share lets a row through that the save path rejects. Where the two disagree, the stricter wins.
    ///
    /// The family groupings below are exactly the frontend's LAND_TYPES / BUILDING_REQUIRED_TYPES /
    /// CONDO_TYPES / OWNER_NAME_TYPES code lists, expressed as families instead of code lists.
    /// </summary>
    private static void ValidateRequiredForFamily(RowContext ctx, string family, Dictionary<string, object?> values)
    {
        var isLand = family is "L" or "LB" or "LS" or "LSL";
        var isBuilding = family is "B" or "LB" or "LS" or "LSB";
        var isCondo = family is "U" or "LSU";

        // Both addresses need a house number: the form marks it required on every collateral type,
        // movables included.
        ctx.Require(values, "titleAddress.houseNumber");
        ctx.Require(values, "dopaAddress.houseNumber");

        // OwnerName is required by every land/building/condo/lease variant, but not by the movables.
        if (isLand || isBuilding || isCondo)
            ctx.Require(values, "ownerName");

        // The form requires the title-detail note for land and condo (not for buildings or movables).
        if (isLand || isCondo)
            ctx.Require(values, "notes");

        if (isLand || isCondo)
        {
            ctx.Require(values, "titleNumber");
            ctx.Require(values, "titleType");

            var deedType = Str(values, "titleType");
            if (!string.IsNullOrWhiteSpace(deedType) && !ValidDeedTypes.Contains(deedType))
                ctx.Error("titleType",
                    $"'{deedType}' is not a supported title type. Use one of: {string.Join(", ", ValidDeedTypes)}.");
        }

        if (isLand)
        {
            // LandArea.Validate rejects a zero total, so at least one of the three must be present.
            var totalWa = (Int(values, "areaRai") ?? 0) * 400m
                          + (Int(values, "areaNgan") ?? 0) * 100m
                          + (Dec(values, "areaSquareWa") ?? 0m);

            // Suppress the "area is required" complaint only when a cell was filled in and failed to
            // parse — that already produced its own error, and two contradictory messages on one row
            // help nobody. An explicit 0 / 0 / 0 parses fine and must still be rejected, because
            // LandArea.Validate() throws on a zero total when the request is saved.
            var areaUnparseable =
                (ctx.HasText("areaRai") && Int(values, "areaRai") is null) ||
                (ctx.HasText("areaNgan") && Int(values, "areaNgan") is null) ||
                (ctx.HasText("areaSquareWa") && Dec(values, "areaSquareWa") is null);

            if (totalWa <= 0 && !areaUnparseable)
                ctx.Error("areaSquareWa", "Land area is required — fill in at least one of Rai, Ngan or Sq. Wa.");
        }

        if (isBuilding)
        {
            ctx.Require(values, "buildingType");

            // Usable area is NOT required here. BuildingInfo.Validate() only checks BuildingType, and
            // the form's effective rule is condo-only: `usableArea` is declared twice in fields.ts and
            // deduplicateByName keeps the first, which is titleBuildingFields' CONDO_TYPES version.
        }

        if (isCondo)
        {
            ctx.Require(values, "condoName");
            ctx.Require(values, "buildingNumber");
            ctx.Require(values, "condoRegistrationNumber");
            ctx.Require(values, "roomNumber");
            ctx.Require(values, "floorNumber");

            if (Dec(values, "usableArea") is null && !ctx.HasText("usableArea"))
                ctx.Error("usableArea", "Usable area is required.");
        }

        switch (family)
        {
            case "VEH":
                ctx.Require(values, "vin");
                ctx.Require(values, "vehicleType");
                ctx.Require(values, "licensePlateNumber");
                break;
            case "VES":
                ctx.Require(values, "hin");
                break;
            case "MAC":
                ctx.Require(values, "registrationNumber");
                ctx.Require(values, "machineType");
                ctx.Require(values, "installationStatus");

                // Status "2" is "being purchased", where the invoice is what stands in for the
                // machine — the form makes it required in exactly that case.
                if (Str(values, "installationStatus") == "2")
                    ctx.Require(values, "invoiceNumber");

                if (Int(values, "numberOfMachine") is null && !ctx.HasText("numberOfMachine"))
                    ctx.Error("numberOfMachine", "Number of machines is required.");
                break;
        }
    }

    // ── Addresses ────────────────────────────────────────────────────────────

    private record ResolvedAddress(AddressDto Dto, string? SubDistrictName, string? DistrictName, string? ProvinceName);

    private async Task<ResolvedAddress> ResolveAddressAsync(
        RowContext ctx, string prefix, bool isDopa, CancellationToken cancellationToken)
    {
        var province = ctx.Text($"{prefix}.province");
        var district = ctx.Text($"{prefix}.district");
        var subDistrict = ctx.Text($"{prefix}.subDistrict");

        var resolution = isDopa
            ? await addressLookup.ResolveDopaAsync(province, district, subDistrict, cancellationToken)
            : await addressLookup.ResolveTitleAsync(province, district, subDistrict, cancellationToken);

        var label = isDopa ? "DOPA address" : "title address";

        // Address.Validate() demands sub-district, district, province and postcode on BOTH addresses
        // for every collateral type, so a blank block is an error rather than an omission.
        switch (resolution.Status)
        {
            case AddressResolutionStatus.Empty:
                ctx.Error($"{prefix}.subDistrict",
                    $"The {label} is required (sub-district, district and province).");
                break;

            case AddressResolutionStatus.NotFound:
                ctx.Error($"{prefix}.subDistrict", Describe(
                    $"No {label} matches '{Join(subDistrict, district, province)}' in the " +
                    (isDopa ? "DOPA" : "Land Department") + " address master",
                    resolution.Candidates));
                break;

            case AddressResolutionStatus.Ambiguous:
                ctx.Error($"{prefix}.subDistrict", Describe(
                    $"The {label} '{Join(subDistrict, district, province)}' matches more than one locality — " +
                    "add the district and province to narrow it down",
                    resolution.Candidates));
                break;
        }

        var matched = resolution.Matched;
        var dto = new AddressDto(
            HouseNumber: ctx.Text($"{prefix}.houseNumber"),
            ProjectName: ctx.Text($"{prefix}.projectName"),
            Moo: ctx.Text($"{prefix}.moo"),
            Soi: ctx.Text($"{prefix}.soi"),
            Road: ctx.Text($"{prefix}.road"),
            SubDistrict: matched?.SubDistrictCode,
            District: matched?.DistrictCode,
            Province: matched?.ProvinceCode,
            Postcode: matched?.Postcode);

        // Postcode is nullable on the Title master (3,715 rows have none) but Address.Validate
        // requires it, so a matched row without one still cannot be saved. Say so here.
        if (matched is not null && string.IsNullOrWhiteSpace(matched.Postcode))
            ctx.Error($"{prefix}.subDistrict",
                $"The {label} '{matched.SubDistrictName}' has no postcode in the address master, so this row " +
                "cannot be saved. Check the sub-district spelling, or ask an administrator to add its postcode.");

        return new ResolvedAddress(dto, matched?.SubDistrictName, matched?.DistrictName, matched?.ProvinceName);
    }

    private static string Join(params string?[] parts) =>
        string.Join(" / ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string Describe(string message, IReadOnlyList<ParameterAddressDto> candidates)
        => candidates.Count == 0
            ? message
            : $"{message}. Did you mean: {string.Join(", ", candidates.Select(c => $"{c.SubDistrictName}/{c.DistrictName}/{c.ProvinceName}"))}?";

    // ── Parameter codes ──────────────────────────────────────────────────────

    private async Task<Dictionary<string, IReadOnlySet<string>>> LoadParameterCodesAsync(CancellationToken ct)
    {
        var result = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in TitleImportCatalog.ParameterGroups())
        {
            var codes = await parameterLookup.GetValidCodesAsync(
                new ParameterDto(
                    ParId: null, Group: group, Country: null, Language: null,
                    Code: null, Description: null, IsActive: true, SeqNo: null),
                ct);

            result[group] = codes;
        }

        return result;
    }

    private static string? Str(Dictionary<string, object?> values, string key)
        => values.GetValueOrDefault(key) as string;

    private static int? Int(Dictionary<string, object?> values, string key)
        => values.GetValueOrDefault(key) as int?;

    private static decimal? Dec(Dictionary<string, object?> values, string key)
        => values.GetValueOrDefault(key) as decimal?;

    private static Dictionary<string, int> BuildHeaderMap(RawSheet sheet, TitleImportSheet definition)
    {
        // Header text → its column index, then field key → that index. A header the sheet does not
        // define is ignored rather than rejected: users add their own working columns all the time.
        var byHeader = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var c = 0; c < sheet.Headers.Count; c++)
        {
            var key = TitleImportCatalog.Normalize(sheet.Headers[c]);
            if (key.Length > 0) byHeader.TryAdd(key, c);
        }

        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var column in definition.Columns)
        {
            foreach (var header in column.AllHeaders())
            {
                if (!byHeader.TryGetValue(TitleImportCatalog.Normalize(header), out var index)) continue;
                map[column.Key] = index;
                break;
            }
        }

        return map;
    }

    /// <summary>Everything one row needs: its sheet definition, its cells, and where to put complaints.</summary>
    private sealed class RowContext(
        TitleImportSheet sheet,
        IReadOnlyDictionary<string, int> headerMap,
        IReadOnlyList<string> cells,
        string sheetName,
        int rowNumber,
        List<TitleImportRowError> errors)
    {
        public TitleImportSheet Sheet => sheet;
        public string SheetName => sheetName;
        public int RowNumber => rowNumber;
        public List<TitleImportRowError> Errors => errors;

        public string Text(string key)
        {
            if (!headerMap.TryGetValue(key, out var index) || index >= cells.Count) return string.Empty;
            return cells[index].Trim();
        }

        /// <summary>Did the user put anything in this cell, regardless of whether it parsed?</summary>
        public bool HasText(string key) => !string.IsNullOrWhiteSpace(Text(key));

        public void Error(string columnKey, string message)
            => errors.Add(new TitleImportRowError(sheetName, rowNumber, LabelOf(columnKey), message));

        /// <summary>
        /// Reports a missing value — but stays quiet when the cell was filled in and rejected during
        /// parsing. Parse returns null for both "blank" and "bad", so without this check an
        /// unparseable owner name reads "'…' is longer than 100 characters" AND "Owner is required.",
        /// which contradict each other and bury the actionable one.
        /// </summary>
        public void Require(Dictionary<string, object?> values, string key)
        {
            if (values.GetValueOrDefault(key) is string s && !string.IsNullOrWhiteSpace(s)) return;
            if (values.GetValueOrDefault(key) is not null and not string) return;
            if (HasText(key)) return;
            Error(key, $"{LabelOf(key)} is required.");
        }

        public string LabelOf(string columnKey)
            => sheet.Columns.FirstOrDefault(c => c.Key == columnKey)?.Label ?? columnKey;

        /// <summary>Cell → typed value, reporting anything unparseable or out of range.</summary>
        public object? Parse(TitleImportColumn column, IReadOnlyDictionary<string, IReadOnlySet<string>> validCodes)
        {
            var raw = Text(column.Key);
            if (string.IsNullOrWhiteSpace(raw)) return null;

            switch (column.Kind)
            {
                case TitleImportColumnKind.Boolean:
                {
                    var lowered = raw.Trim().ToLowerInvariant();
                    if (TruthyValues.Contains(lowered)) return true;
                    if (FalsyValues.Contains(lowered)) return false;
                    Error(column.Key, $"'{raw}' is not a yes/no value — use Y or N.");
                    return null;
                }

                case TitleImportColumnKind.Integer:
                {
                    if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    {
                        Error(column.Key, $"'{raw}' is not a number.");
                        return null;
                    }

                    if (d != Math.Truncate(d))
                    {
                        Error(column.Key, $"'{raw}' must be a whole number.");
                        return null;
                    }

                    // Range-check while it is still a decimal: converting decimal to int is always
                    // checked in C#, so casting first turns a fat-fingered "99999999999" into an
                    // OverflowException that escapes the row loop and 500s the entire upload.
                    if (!CheckRange(column, d)) return null;

                    if (d < int.MinValue || d > int.MaxValue)
                    {
                        Error(column.Key, $"'{raw}' is too large.");
                        return null;
                    }

                    return (int)d;
                }

                case TitleImportColumnKind.Decimal:
                {
                    if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                    {
                        Error(column.Key, $"'{raw}' is not a number.");
                        return null;
                    }

                    value = Math.Round(value, column.DecimalPlaces, MidpointRounding.AwayFromZero);
                    return CheckRange(column, value) ? value : null;
                }

                default:
                {
                    // A code column may arrive as "01 — Land" straight from the template's dropdown;
                    // everything downstream wants the bare code.
                    var text = column.ParameterGroup is null ? raw : TitleImportCatalog.StripCodeLabel(raw);

                    if (column.MaxLength is { } max && text.Length > max)
                    {
                        Error(column.Key, $"Longer than {max} characters (currently {text.Length}).");
                        return null;
                    }

                    if (!column.CheckedByDomain &&
                        column.ParameterGroup is { } group &&
                        validCodes.TryGetValue(group, out var codes) &&
                        !codes.Contains(text))
                    {
                        Error(column.Key, $"'{text}' is not a valid code. See the Reference sheet for the valid list.");
                        return null;
                    }

                    return text;
                }
            }
        }

        private bool CheckRange(TitleImportColumn column, decimal value)
        {
            if (column.Min is { } min && value < min)
            {
                Error(column.Key, $"Must not be less than {min}.");
                return false;
            }

            if (column.Max is { } max && value > max)
            {
                Error(column.Key, $"Must not be greater than {max}.");
                return false;
            }

            // Mirrors the frontend's maxIntegerDigits rule, so an imported row also passes the
            // on-screen form's own schema — otherwise it lands in the list already invalid.
            if (column.MaxIntegerDigits is { } digits &&
                Math.Truncate(Math.Abs(value)).ToString(CultureInfo.InvariantCulture).Length > digits)
            {
                Error(column.Key, $"Must not exceed {digits} digits before the decimal point.");
                return false;
            }

            return true;
        }
    }
}
