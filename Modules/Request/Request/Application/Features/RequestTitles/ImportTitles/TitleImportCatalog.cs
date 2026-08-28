namespace Request.Application.Features.RequestTitles.ImportTitles;

using Kind = TitleImportColumnKind;

/// <summary>
/// One worksheet of the import workbook: which collateral families it accepts and which columns it carries.
/// </summary>
/// <param name="Key">Worksheet name in the generated template.</param>
/// <param name="Name">Human name, shown in the Instructions sheet and in error messages.</param>
/// <param name="Aliases">Other worksheet names accepted when the user brings their own file, including the Thai wording.</param>
/// <param name="Families">
/// TitleFamily codes this sheet may contain, as produced by <see cref="CollateralType.FamilyCode"/>.
/// A row whose collateral type belongs to a different family is rejected — pasting car rows into the
/// Land sheet is a mistake worth reporting, not something to silently accept.
/// </param>
public record TitleImportSheet(
    string Key,
    string Name,
    string[] Aliases,
    string[] Families,
    IReadOnlyList<TitleImportColumn> Columns);

/// <summary>
/// The column layout of every import sheet.
///
/// Required-ness is NOT declared here: it depends on the collateral type and lives in
/// <see cref="TitleImportValidator"/>, which mirrors the domain's own Validate() methods
/// (TitleLand.Validate, CondoInfo.Validate, VehicleInfo.Validate …) as well as the on-screen form's
/// schema. Keeping it in one place means an imported row can never pass import and then fail on Save.
///
/// Headers are written in English and Thai spellings are accepted as aliases, so a file prepared from
/// the Thai labels shown in the UI still maps onto the right columns.
/// </summary>
public static class TitleImportCatalog
{
    // ── Shared blocks ────────────────────────────────────────────────────────

    private static readonly TitleImportColumn CollateralTypeColumn = new(
        "collateralType", "Collateral Type Code",
        ["CollateralType", "ประเภทหลักประกัน", "ประเภทหลักประกัน (รหัส)", "รหัสประเภทหลักประกัน"],
        // The group is declared so the Reference sheet lists the codes, but the value itself is
        // checked by CollateralType.TryFromCode — that knows the sheet/family rule too and says
        // something useful. Running both would report the same bad cell twice.
        ParameterGroup: "CollateralType", CheckedByDomain: true, MaxLength: 10, ForceTextFormat: true,
        Hint: "Two-digit code. See the Reference sheet for the full list.");

    private static readonly TitleImportColumn OwnerNameColumn = new(
        "ownerName", "Owner", ["OwnerName", "เจ้าของ", "ชื่อเจ้าของ"], MaxLength: 100);

    private static readonly TitleImportColumn NotesColumn = new(
        // The form calls this "Title Detail", which reads oddly on the Vehicle/Vessel/Machine sheets;
        // the neutral label carries better and the form's own wording stays accepted as an alias.
        "notes", "Notes", ["Title Detail", "รายละเอียด/หมายเหตุ", "รายละเอียดโฉนด", "หมายเหตุ"],
        MaxLength: 200);

    private static readonly TitleImportColumn CollateralStatusColumn = new(
        "collateralStatus", "Previous Appraisal / CAS Status",
        ["CollateralStatus", "เลขที่รายงาน/สถานะ CAS"], Kind: Kind.Boolean,
        Hint: "Y/N or TRUE/FALSE. Leave blank for N.");

    private static IEnumerable<TitleImportColumn> AddressBlock(
        string prefix, string labelPrefix, string thaiPrefix) =>
    [
        new($"{prefix}.houseNumber", $"{labelPrefix} House No",
            [$"{thaiPrefix}บ้านเลขที่"], MaxLength: 10, ForceTextFormat: true),
        new($"{prefix}.projectName", $"{labelPrefix} Village/Building",
            [$"{thaiPrefix}หมู่บ้าน/อาคาร"], MaxLength: 100),
        new($"{prefix}.moo", $"{labelPrefix} Moo",
            [$"{thaiPrefix}หมู่"], MaxLength: 10, ForceTextFormat: true),
        // 50, not the form's 100: RequestTitleConfiguration maps both as nvarchar(50), and the
        // smaller cap is the one that actually rejects the value — at SQL, taking the whole request
        // down with it, if it is not caught here first.
        new($"{prefix}.soi", $"{labelPrefix} Soi",
            [$"{thaiPrefix}ซอย"], MaxLength: 50),
        new($"{prefix}.road", $"{labelPrefix} Road",
            [$"{thaiPrefix}ถนน"], MaxLength: 50),
        new($"{prefix}.subDistrict", $"{labelPrefix} Sub District",
            [$"{thaiPrefix}ตำบล/แขวง"], MaxLength: 100,
            Hint: "Thai name, or the 6-digit sub-district code."),
        new($"{prefix}.district", $"{labelPrefix} District",
            [$"{thaiPrefix}อำเภอ/เขต"], MaxLength: 100),
        new($"{prefix}.province", $"{labelPrefix} Province",
            [$"{thaiPrefix}จังหวัด"], MaxLength: 100)
        // Postcode is deliberately absent: it is derived from the resolved sub-district,
        // exactly as the on-screen form does (the postcode input there is disabled).
    ];

    // The two addresses are genuinely different data and are mastered by different tables
    // (Title = Land Department, DOPA = Department of Provincial Administration). Never copy one
    // into the other — 3,715 Title sub-districts have no DOPA counterpart at all.
    private static IEnumerable<TitleImportColumn> TitleAddress() =>
        AddressBlock("titleAddress", "Title Address:", "ที่อยู่ตามโฉนด: ");

    private static IEnumerable<TitleImportColumn> DopaAddress() =>
        AddressBlock("dopaAddress", "DOPA Address:", "ที่อยู่ตามทะเบียนราษฎร์: ");

    // ── Type-specific blocks ─────────────────────────────────────────────────

    private static IEnumerable<TitleImportColumn> DeedBlock() =>
    [
        new("titleType", "Title Type", ["DeedType", "ประเภทโฉนด", "ชนิดเอกสารสิทธิ์"],
            ParameterGroup: "DeedType", MaxLength: 50, ForceTextFormat: true,
            Hint: "DEED / NS3 / NS3K / NS3KO / POSR / OTHER"),
        new("titleNumber", "Title Number", ["TitleNo", "เลขที่โฉนด", "เลขโฉนด"],
            MaxLength: 500, ForceTextFormat: true)
    ];

    private static IEnumerable<TitleImportColumn> LandBlock() =>
    [
        new("bookNumber", "Book Number", ["BookNo", "เล่มที่"], MaxLength: 10, ForceTextFormat: true),
        new("pageNumber", "Page Number", ["PageNo", "หน้าที่"], MaxLength: 10, ForceTextFormat: true),
        // Rawang is the deed's map sheet; MapSheetNumber is the NS3K "sheet number". The two read as
        // synonyms in English and picking the wrong one fails silently, so both are spelled out.
        new("rawang", "Rawang", ["ระวาง", "ระวางโฉนด"], MaxLength: 30, ForceTextFormat: true,
            Hint: "Used for title deeds (DEED)."),
        new("mapSheetNumber", "Sheet Number", ["MapSheetNo", "แผ่นที่"], MaxLength: 10, ForceTextFormat: true,
            Hint: "Used for NS3K."),
        new("aerialMapName", "Aerial Map Name", ["ชื่อระวางรูปถ่ายทางอากาศ"], MaxLength: 100),
        new("aerialMapNumber", "Aerial Map Number", ["หมายเลขระวางรูปถ่ายทางอากาศ"],
            MaxLength: 50, ForceTextFormat: true),
        new("landParcelNumber", "Land Parcel Number", ["LandNo", "เลขที่ดิน"], MaxLength: 10, ForceTextFormat: true),
        new("surveyNumber", "Survey Number", ["SurveyNo", "หน้าสำรวจ"], MaxLength: 10, ForceTextFormat: true),
        new("areaRai", "Area (Rai)", ["Rai", "เนื้อที่ (ไร่)", "ไร่"],
            Kind: Kind.Integer, MaxIntegerDigits: 5, Min: 0),
        new("areaNgan", "Area (Ngan)", ["Ngan", "เนื้อที่ (งาน)", "งาน"],
            Kind: Kind.Integer, MaxIntegerDigits: 1, Min: 0, Max: 3),
        new("areaSquareWa", "Area (Sq. Wa)", ["SqWa", "เนื้อที่ (ตร.ว.)", "ตารางวา", "ตร.ว."],
            Kind: Kind.Decimal, MaxIntegerDigits: 2, DecimalPlaces: 2, Min: 0)
    ];

    private static IEnumerable<TitleImportColumn> BuildingBlock() =>
    [
        new("buildingType", "Building Type", ["ประเภทอาคาร"], ParameterGroup: "BuildingType",
            MaxLength: 10, ForceTextFormat: true),
        new("usableArea", "Usable Area (sq.m.)", ["UsageArea", "พื้นที่ใช้สอย (ตร.ม.)", "พื้นที่ใช้สอย"],
            Kind: Kind.Decimal, MaxIntegerDigits: 3, DecimalPlaces: 2, Min: 0),
        new("numberOfBuilding", "Number of Buildings", ["จำนวนอาคาร"],
            Kind: Kind.Integer, MaxIntegerDigits: 5, Min: 0)
    ];

    private static IEnumerable<TitleImportColumn> CondoBlock() =>
    [
        new("condoName", "Condo Name", ["ชื่อคอนโด"], MaxLength: 100),
        new("buildingNumber", "Building Number", ["BuildingNo", "หมายเลขอาคาร"],
            MaxLength: 30, ForceTextFormat: true),
        new("condoRegistrationNumber", "Condo Registration Number",
            ["CondoRegistrationNo", "หมายเลขทะเบียนคอนโด"], MaxLength: 10, ForceTextFormat: true),
        new("roomNumber", "Room Number", ["RoomNo", "หมายเลขห้อง"], MaxLength: 10, ForceTextFormat: true),
        new("floorNumber", "Floor Number", ["FloorNo", "ชั้นที่"], MaxLength: 10, ForceTextFormat: true),
        new("usableArea", "Usable Area (sq.m.)", ["UsageArea", "พื้นที่ใช้สอย (ตร.ม.)", "พื้นที่ใช้สอย"],
            Kind: Kind.Decimal, MaxIntegerDigits: 3, DecimalPlaces: 2, Min: 0)
    ];

    private static IEnumerable<TitleImportColumn> VehicleBlock() =>
    [
        new("vehicleType", "Vehicle Type", ["ประเภทยานพาหนะ"], ParameterGroup: "VehicleType",
            MaxLength: 10, ForceTextFormat: true),
        new("vin", "Chassis Number", ["VIN", "หมายเลขตัวถัง"], MaxLength: 50, ForceTextFormat: true),
        new("licensePlateNumber", "License Plate Number", ["ทะเบียนรถ"], MaxLength: 20, ForceTextFormat: true),
        new("vehicleLocation", "Appointment Location", ["สถานที่นัดหมาย"], MaxLength: 200)
    ];

    private static IEnumerable<TitleImportColumn> VesselBlock() =>
    [
        new("vesselType", "Vessel Type", ["ประเภทเรือ"], ParameterGroup: "VesselType",
            MaxLength: 10, ForceTextFormat: true),
        new("hin", "Hull Number", ["HIN", "หมายเลขตัวเรือ"], MaxLength: 50, ForceTextFormat: true),
        new("vesselRegistrationNumber", "Vessel Registration Number", ["ทะเบียนเรือ"],
            MaxLength: 50, ForceTextFormat: true),
        new("vesselLocation", "Appointment Location", ["สถานที่นัดหมาย"], MaxLength: 200)
    ];

    private static IEnumerable<TitleImportColumn> MachineBlock() =>
    [
        new("installationStatus", "Machine Status", ["สถานะเครื่องจักร"], ParameterGroup: "MachineStatus",
            MaxLength: 10, ForceTextFormat: true),
        new("machineType", "Machine Type", ["ประเภทเครื่องจักร"], ParameterGroup: "MachineType",
            MaxLength: 10, ForceTextFormat: true),
        new("registrationStatus", "Registration Status", ["สถานะการจดทะเบียน"], Kind: Kind.Boolean,
            Hint: "Y = registered, N = not registered."),
        new("registrationNumber", "Registration Number", ["RegistrationNo", "เลขทะเบียนเครื่องจักร"],
            MaxLength: 50, ForceTextFormat: true),
        new("invoiceNumber", "Invoice Number", ["InvoiceNo", "เลขที่ใบแจ้งหนี้"],
            MaxLength: 20, ForceTextFormat: true),
        new("numberOfMachine", "Number of Machines", ["NoOfMachines", "จำนวนเครื่องจักร"],
            Kind: Kind.Integer, MaxIntegerDigits: 5, Min: 0)
    ];

    /// <summary>
    /// Assembles one sheet from its type-specific blocks plus the blocks every row carries.
    /// </summary>
    /// <param name="typeBlocks">
    /// Each block paired with the collateral types its columns are for. On the Property sheet four
    /// types share one column set, so that note is the only thing telling a user which of the 41
    /// columns concern their row; it is surfaced on the template's Reference sheet.
    /// </param>
    private static TitleImportSheet Sheet(
        string key, string name, string[] aliases, string[] families,
        params (string AppliesTo, IEnumerable<TitleImportColumn> Columns)[] typeBlocks)
    {
        (string AppliesTo, IEnumerable<TitleImportColumn> Columns)[] blocks =
        [
            ("All", [CollateralTypeColumn]),
            ..typeBlocks,
            ("All", [OwnerNameColumn]),
            ("All", [NotesColumn]),
            ("All", TitleAddress()),
            ("All", DopaAddress()),
            ("All", [CollateralStatusColumn])
        ];

        var ordered = new List<TitleImportColumn>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (appliesTo, columns) in blocks)
        {
            foreach (var column in columns)
            {
                // A column can belong to more than one block — usableArea is both a building and a
                // condo field. Keep its first position and widen the applicability note.
                if (seen.TryGetValue(column.Key, out var at))
                {
                    if (ordered[at].AppliesTo != appliesTo)
                        ordered[at] = ordered[at] with { AppliesTo = $"{ordered[at].AppliesTo}, {appliesTo}" };
                    continue;
                }

                seen[column.Key] = ordered.Count;
                ordered.Add(column with { AppliesTo = appliesTo });
            }
        }

        return new TitleImportSheet(key, name, aliases, families, ordered);
    }

    public static readonly IReadOnlyList<TitleImportSheet> Sheets =
    [
        // One sheet for all real estate: the four types share the deed, address and owner columns
        // anyway, so splitting them cost four sheets to save eight blank cells on a land row — and
        // made the user decide up front whether a plot with a house on it counted as "Land" or
        // "LandBuilding". The Collateral Type Code column already answers that.
        Sheet("Property", "Property (land, buildings, condominium)",
            ["Land", "LandBuilding", "LandAndBuilding", "Building", "Condo", "Condominium",
             "อสังหาริมทรัพย์", "ที่ดิน", "ที่ดินพร้อมสิ่งปลูกสร้าง", "สิ่งปลูกสร้าง", "อาคาร", "ห้องชุด", "คอนโด"],
            ["L", "LB", "B", "LS", "LSL", "LSB", "U", "LSU"],
            ("Land, Land with buildings, Condominium unit", DeedBlock()),
            ("Land, Land with buildings", LandBlock()),
            ("Land with buildings, Buildings", BuildingBlock()),
            ("Condominium unit", CondoBlock())),

        // The movables keep their own sheets: not one of their fields overlaps with real estate, so
        // folding them in would add 13 permanently blank columns to every property row.
        Sheet("Vehicle", "Vehicle", ["Car", "รถ", "ยานพาหนะ"], ["VEH"],
            ("Vehicle", VehicleBlock())),

        Sheet("Vessel", "Vessel", ["Ship", "เรือ"], ["VES"],
            ("Vessel", VesselBlock())),

        Sheet("Machine", "Machinery", ["Machinery", "เครื่องจักร"], ["MAC"],
            ("Machinery", MachineBlock()))
    ];

    /// <summary>Names of the template's own non-data sheets.</summary>
    public const string ReferenceSheetName = "Reference";
    public const string InstructionsSheetName = "Instructions";

    /// <summary>Hidden sheet holding the dropdown sources the code columns point at.</summary>
    public const string ListsSheetName = "Lists";

    /// <summary>
    /// Separator between a code and its description in the template's dropdowns, e.g. "01 — Land".
    ///
    /// The cell has to end up holding the code, but a dropdown of bare codes is unreadable when the
    /// list runs to 33 entries, so the label rides along and is stripped on the way back in.
    /// </summary>
    public const string CodeLabelSeparator = " — ";

    /// <summary>
    /// Reduces a picked "01 — Land" back to "01", and leaves a hand-typed "01" alone.
    ///
    /// Both forms have to work: the dropdown is a convenience, not a requirement, and anyone
    /// preparing a file from another system will write bare codes.
    /// </summary>
    public static string StripCodeLabel(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0) return text;

        foreach (var separator in (string[])[CodeLabelSeparator, " - ", " – "])
        {
            var at = text.IndexOf(separator, StringComparison.Ordinal);
            if (at > 0) return text[..at].Trim();
        }

        return text;
    }

    /// <summary>
    /// True for a sheet the template itself ships that carries no title rows.
    ///
    /// They come back on every upload of an unmodified template, so reporting them as "unrecognised"
    /// would tell every user that something is wrong with a file the system just handed them.
    /// </summary>
    public static bool IsTemplateHelperSheet(string? worksheetName)
    {
        var key = Normalize(worksheetName);
        return key == Normalize(ReferenceSheetName)
               || key == Normalize(InstructionsSheetName)
               || key == Normalize(ListsSheetName);
    }

    public static TitleImportSheet? FindSheet(string? worksheetName)
    {
        if (string.IsNullOrWhiteSpace(worksheetName)) return null;
        var key = Normalize(worksheetName);

        return Sheets.FirstOrDefault(s =>
            Normalize(s.Key) == key ||
            Normalize(s.Name) == key ||
            s.Aliases.Any(a => Normalize(a) == key));
    }

    /// <summary>
    /// Header/sheet-name comparison key: whitespace removed, lower-cased.
    /// Whitespace is dropped rather than collapsed so "Title No" and "TitleNo" are the same header —
    /// this is header text, never address data, where an internal space IS meaningful.
    /// </summary>
    public static string Normalize(string? raw)
        => new string((raw ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

    /// <summary>Every parameter group referenced by any sheet — loaded once per import.</summary>
    public static IReadOnlySet<string> ParameterGroups() =>
        Sheets.SelectMany(s => s.Columns)
            .Select(c => c.ParameterGroup)
            .Where(g => g is not null)
            .Select(g => g!)
            .ToHashSet();
}
