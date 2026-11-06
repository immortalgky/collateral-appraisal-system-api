using NSubstitute;
using Parameter.Contracts.Parameters;
using Request.Application.Features.RequestTitles.ImportTitles;
using Request.Application.Features.RequestTitles.ImportTitles.Reading;
using Request.Application.EventHandlers.Request;
using Request.Domain.Requests;
using Request.Domain.RequestTitles;
using Request.Domain.RequestTitles.TitleTypes;
using Request.Tests.Request.Requests.Models;
using Shared.Exceptions;
using ParameterAddressDto = Parameter.Contracts.Parameters.Dtos.AddressDto;
using ParameterDto = Parameter.Contracts.Parameters.Dtos.ParameterDto;

namespace Request.Tests.Request.RequestTitles;

/// <summary>Movables (vehicle 10, machine 11, vessel 12) have no title-document address; immovables still do.</summary>
public class MovableTitleAddressTests
{
    private static readonly Address EmptyAddress = Address.Create(new AddressData(
        null, null, null, null, null, null, null, null, null));

    private static RequestTitle Title(string code, Address? titleAddress)
    {
        var data = new RequestTitleDataBuilder().WithCollateralType(code).Build() with
        {
            TitleAddress = titleAddress!
        };
        return TitleFactory.Create(code, data);
    }

    [Theory]
    [InlineData("10", typeof(TitleVehicle))]
    [InlineData("11", typeof(TitleMachine))]
    [InlineData("12", typeof(TitleVessel))]
    public void Movable_validates_with_null_or_empty_title_address(string code, Type expected)
    {
        Assert.IsType(expected, Title(code, null));

        Title(code, null).Validate();
        Title(code, EmptyAddress).Validate();
    }

    [Fact]
    public void Movable_still_requires_dopa_address()
    {
        var data = new RequestTitleDataBuilder().WithCollateralType("10").Build() with
        {
            TitleAddress = null!,
            DopaAddress = EmptyAddress
        };

        Assert.Throws<DomainException>(() => TitleFactory.Create("10", data).Validate());
    }

    [Fact]
    public void Land_without_title_address_still_fails()
    {
        Assert.Throws<ArgumentNullException>(Title("01", null).Validate);
    }

    [Fact]
    public void Land_with_empty_title_address_still_fails()
    {
        Assert.Throws<DomainException>(Title("01", EmptyAddress).Validate);
    }

    [Fact]
    public void Submitted_event_mapping_copes_with_null_title_address()
    {
        var dto = RequestSubmittedEventHandler.MapToDto(Title("10", null));

        Assert.NotNull(dto.TitleAddress);
        Assert.Null(dto.TitleAddress.SubDistrict);
        Assert.NotNull(dto.DopaAddress.SubDistrict);
    }

    [Fact]
    public void Submitted_event_mapping_carries_movable_type_fields()
    {
        var vehicle = RequestSubmittedEventHandler.MapToDto(Title("10", null));
        Assert.Equal("Car", vehicle.VehicleType);
        Assert.Equal("REGIS-123", vehicle.VehicleRegistrationNumber);
        Assert.Equal("Location", vehicle.VehicleLocation);
        Assert.Equal("VIN123", vehicle.VIN);
        Assert.Equal("ABC-1234", vehicle.LicensePlateNumber);

        var vessel = RequestSubmittedEventHandler.MapToDto(Title("12", null));
        Assert.Equal("Boat", vessel.VesselType);
        Assert.Equal("Marina", vessel.VesselLocation);
        Assert.Equal("HIN123", vessel.HIN);
        Assert.Equal("VES-001", vessel.VesselRegistrationNumber);

        var machine = RequestSubmittedEventHandler.MapToDto(Title("11", null));
        Assert.True(machine.RegistrationStatus);
        Assert.Equal("REG123", machine.RegistrationNumber);
        Assert.Equal("Type", machine.MachineType);
        Assert.Equal("Installed", machine.InstallationStatus);
        Assert.Equal("INV123", machine.InvoiceNumber);
        Assert.Equal(1, machine.NumberOfMachine);
    }

    private static TitleImportValidator ImportValidator()
    {
        var parameters = Substitute.For<IParameterLookupService>();
        parameters.GetValidCodesAsync(Arg.Any<ParameterDto>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string> { "1", "01", "DEED" });

        var match = new ParameterAddressDto("10", "P", "P", "1001", "D", "D", "100101", "S", "S", "10100");
        var addresses = Substitute.For<IAddressLookupService>();
        addresses.ResolveTitleAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(AddressResolution.Found(match));
        addresses.ResolveDopaAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(AddressResolution.Found(match));

        return new TitleImportValidator(parameters, addresses);
    }

    /// <summary>
    /// One data row on the named catalog sheet, headers being the column keys. Extra columns are
    /// appended as given, to stand in for a title address left over from an older template.
    /// </summary>
    private static RawSheet Sheet(
        string key, Dictionary<string, string> values, Dictionary<string, string>? extra = null,
        Func<string, bool>? dropColumn = null)
    {
        extra ??= [];
        var columns = TitleImportCatalog.Sheets.Single(s => s.Key == key).Columns
            .Select(c => c.Key)
            .Where(k => dropColumn is null || !dropColumn(k))
            .ToList();
        List<string> headers = [.. columns, .. extra.Keys];
        List<string> cells =
            [.. columns.Select(k => values.GetValueOrDefault(k, "")), .. extra.Values];
        return new RawSheet(key, headers, [cells], 1);
    }

    private static Dictionary<string, string> VehicleValues() => new()
    {
        ["collateralType"] = "10",
        ["vin"] = "VIN1",
        ["vehicleType"] = "1",
        ["vehicleRegistrationNumber"] = "REG-1",
        ["dopaAddress.houseNumber"] = "9",
        ["dopaAddress.subDistrict"] = "S"
    };

    [Fact]
    public async Task Vehicle_row_with_registration_number_and_no_plate_imports()
    {
        Assert.Contains(
            TitleImportCatalog.Sheets.Single(s => s.Key == "Vehicle").Columns,
            c => c.Key == "vehicleRegistrationNumber" && c.Label == "Vehicle Registration Number");

        var result = await ImportValidator().ValidateAsync(
            [Sheet("Vehicle", VehicleValues())], CancellationToken.None);

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal("REG-1", row.Title.VehicleRegistrationNumber);
        Assert.Null(row.Title.LicensePlateNumber);
    }

    [Fact]
    public async Task Vehicle_row_without_registration_number_fails()
    {
        var values = VehicleValues();
        values.Remove("vehicleRegistrationNumber");

        var result = await ImportValidator().ValidateAsync(
            [Sheet("Vehicle", values)], CancellationToken.None);

        Assert.Empty(result.Rows);
        Assert.Contains(result.Errors, e => e.Message == "Vehicle Registration Number is required.");
    }

    [Fact]
    public async Task Movable_sheet_has_no_title_address_columns()
    {
        Assert.DoesNotContain(
            TitleImportCatalog.Sheets.Single(s => s.Key == "Vehicle").Columns,
            c => c.Key.StartsWith("titleAddress.", StringComparison.Ordinal));
        Assert.Contains(
            TitleImportCatalog.Sheets.Single(s => s.Key == "Property").Columns,
            c => c.Key == "titleAddress.houseNumber");

        var result = await ImportValidator().ValidateAsync(
            [Sheet("Vehicle", VehicleValues())], CancellationToken.None);

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Null(row.Title.TitleAddress.SubDistrict);
    }

    [Fact]
    public async Task Old_template_movable_sheet_with_filled_title_address_imports_with_it_ignored()
    {
        var oldTemplateCells = new Dictionary<string, string>
        {
            ["titleAddress.houseNumber"] = "5",
            ["titleAddress.subDistrict"] = "S",
            ["titleAddress.district"] = "D",
            ["titleAddress.province"] = "P"
        };

        var result = await ImportValidator().ValidateAsync(
            [Sheet("Vehicle", VehicleValues(), oldTemplateCells)], CancellationToken.None);

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Null(row.Title.TitleAddress.HouseNumber);
        Assert.Null(row.Title.TitleAddress.SubDistrict);
    }

    [Fact]
    public async Task Property_sheet_still_requires_title_address_columns()
    {
        var values = new Dictionary<string, string>
        {
            ["collateralType"] = "01",
            ["ownerName"] = "Owner",
            ["titleNumber"] = "123",
            ["titleType"] = "DEED",
            ["areaSquareWa"] = "50",
            ["dopaAddress.houseNumber"] = "1",
            ["dopaAddress.subDistrict"] = "S"
        };

        await Assert.ThrowsAsync<BadRequestException>(() => ImportValidator().ValidateAsync(
            [Sheet("Property", values, dropColumn: k => k.StartsWith("titleAddress.", StringComparison.Ordinal))],
            CancellationToken.None));
    }

    private static Dictionary<string, string> PropertyValuesWithoutHouseNumbers(string code) => new()
    {
        ["collateralType"] = code,
        ["ownerName"] = "Owner",
        ["titleNumber"] = "123",
        ["titleType"] = "DEED",
        ["areaSquareWa"] = "50",
        ["buildingType"] = "1",
        ["titleAddress.subDistrict"] = "S",
        ["dopaAddress.subDistrict"] = "S"
    };

    [Fact]
    public async Task Land_row_without_house_numbers_imports()
    {
        var result = await ImportValidator().ValidateAsync(
            [Sheet("Property", PropertyValuesWithoutHouseNumbers("01"))], CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }

    [Fact]
    public async Task Building_row_without_house_numbers_still_fails_on_both_addresses()
    {
        var result = await ImportValidator().ValidateAsync(
            [Sheet("Property", PropertyValuesWithoutHouseNumbers("05"))], CancellationToken.None);

        Assert.Empty(result.Rows);
        Assert.Contains(result.Errors, e => e.Column!.Contains("Title Address", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, e => e.Column!.Contains("DOPA Address", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsHouseNumber(string key) => key.EndsWith(".houseNumber", StringComparison.Ordinal);

    [Fact]
    public async Task Land_only_sheet_without_house_number_columns_imports()
    {
        var result = await ImportValidator().ValidateAsync(
            [Sheet("Property", PropertyValuesWithoutHouseNumbers("01"), dropColumn: IsHouseNumber)],
            CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }

    [Fact]
    public async Task Building_row_on_sheet_without_house_number_columns_fails_per_row()
    {
        var result = await ImportValidator().ValidateAsync(
            [Sheet("Property", PropertyValuesWithoutHouseNumbers("05"), dropColumn: IsHouseNumber)],
            CancellationToken.None);

        Assert.Empty(result.Rows);
        Assert.Contains(result.Errors, e => e.Message.Contains("is not in this sheet"));
        Assert.Contains(result.MissingColumns, c => c.Contains("House No", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Require_skips_only_an_undeclared_title_address_block()
    {
        var sheet = new TitleImportSheet("S", "S", [], ["L"], [new TitleImportColumn("ownerName", "Owner")]);
        var errors = new List<TitleImportRowError>();
        var ctx = new TitleImportValidator.RowContext(
            sheet, new Dictionary<string, int>(), [], "S", 2, errors, new SortedSet<string>());
        var values = new Dictionary<string, object?>();

        ctx.Require(values, "titleAddress.houseNumber");
        Assert.Empty(errors);

        ctx.Require(values, "notes");
        ctx.Require(values, "dopaAddress.houseNumber");
        Assert.Equal(
            ["Column 'notes' is not in this sheet.", "Column 'dopaAddress.houseNumber' is not in this sheet."],
            errors.Select(e => e.Message));
    }

    [Fact]
    public async Task Movable_sheet_without_dopa_address_columns_is_still_rejected()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => ImportValidator().ValidateAsync(
            [Sheet("Vehicle", VehicleValues(), dropColumn: k => k.StartsWith("dopaAddress.", StringComparison.Ordinal))],
            CancellationToken.None));
    }
}
