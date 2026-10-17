using NSubstitute;
using Parameter.Contracts.Parameters;
using Parameter.Contracts.Parameters.Dtos;
using Request.Application.Features.RequestTitles.ImportTitles;
using Request.Application.Features.RequestTitles.ImportTitles.Reading;

namespace Request.Tests.Request.RequestTitles;

/// <summary>
/// The title-detail note ("notes") is optional on the form, so the Excel import must not demand it
/// for land or condo rows either, and a template using the form's newer label must still map.
/// </summary>
public class TitleImportNotesTests
{
    private static readonly TitleImportSheet PropertySheet = TitleImportCatalog.Sheets[0];

    private static TitleImportValidator CreateValidator()
    {
        var parameters = Substitute.For<IParameterLookupService>();
        parameters.GetValidCodesAsync(Arg.Any<ParameterDto>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>(["01", "DEED"]));

        var match = new AddressDto("10", "P", "P", "1001", "D", "D", "100101", "S", "S", "10100");
        var addresses = Substitute.For<IAddressLookupService>();
        addresses.ResolveTitleAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(AddressResolution.Found(match));
        addresses.ResolveDopaAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(AddressResolution.Found(match));

        return new TitleImportValidator(parameters, addresses);
    }

    /// <summary>A land row with every required field filled in; headers are the column keys.</summary>
    private static RawSheet LandSheet(string? notesHeader, string notesValue)
    {
        var values = new Dictionary<string, string>
        {
            ["collateralType"] = "01",
            ["ownerName"] = "Owner",
            ["titleNumber"] = "123",
            ["titleType"] = "DEED",
            ["areaSquareWa"] = "50",
            ["titleAddress.houseNumber"] = "1",
            ["titleAddress.subDistrict"] = "S",
            ["dopaAddress.houseNumber"] = "1",
            ["dopaAddress.subDistrict"] = "S",
        };

        var headers = PropertySheet.Columns.Select(c => c.Key == "notes" ? notesHeader ?? "" : c.Key).ToList();
        var cells = PropertySheet.Columns
            .Select(c => c.Key == "notes" ? notesValue : values.GetValueOrDefault(c.Key, ""))
            .ToList();

        return new RawSheet(PropertySheet.Name, headers, [cells], 1);
    }

    [Fact]
    public async Task LandRow_WithoutNotes_ImportsWithNoError()
    {
        var result = await CreateValidator().ValidateAsync(
            [LandSheet(notesHeader: "notes", notesValue: "")], CancellationToken.None);

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.True(string.IsNullOrEmpty(row.Title.Notes));
    }

    [Fact]
    public async Task LandRow_WithNotesColumnAbsent_ImportsWithNoError()
    {
        var result = await CreateValidator().ValidateAsync(
            [LandSheet(notesHeader: null, notesValue: "")], CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }

    [Theory]
    [InlineData("Title Detail (Additional)")]
    [InlineData("รายละเอียดโฉนด (เพิ่มเติม)")]
    public async Task NotesHeader_NewFormLabel_StillMapsToNotes(string header)
    {
        var result = await CreateValidator().ValidateAsync(
            [LandSheet(header, "near the temple")], CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.Equal("near the temple", Assert.Single(result.Rows).Title.Notes);
    }

    [Theory]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public async Task Notes_AreCappedAt4000Characters(int length, bool accepted)
    {
        var result = await CreateValidator().ValidateAsync(
            [LandSheet("notes", new string('x', length))], CancellationToken.None);

        Assert.Equal(accepted, result.Errors.Count == 0);
    }
}
