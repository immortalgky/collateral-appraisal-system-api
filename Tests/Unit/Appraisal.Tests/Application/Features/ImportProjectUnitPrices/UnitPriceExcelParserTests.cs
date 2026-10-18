using Appraisal.Application.Features.Project.ImportProjectUnitPrices;
using Appraisal.Domain.Projects;
using Shared.Exceptions;

namespace Appraisal.Tests.Application.Features.ImportProjectUnitPrices;

public class UnitPriceExcelParserTests
{
    [Fact]
    public void Condo_ReadsKeysPricesAndAttributes()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", "9/101", 1_500_000, 3, "A", "Type1", 35.5, 2_000_000, 40_000, 1_000_000, 500_000));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Empty(sheet.Errors);
        var row = Assert.Single(sheet.Rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal("101", row.Key1);
        Assert.Equal("9/101", row.Key2);
        Assert.Equal(40_000m, row.StandardPrice);
        Assert.Equal(1_500_000m, row.AppraisalValue);
        Assert.Equal(1_000_000m, row.ForceSellingPrice);
        Assert.Equal(500_000m, row.CoverageAmount);
        Assert.Equal(3m, row.Attributes["ชั้นที่"].Number);
        Assert.Equal("A", row.Attributes["อาคาร"].Text);
        Assert.Equal(35.5m, row.Attributes["พื้นที่ใช้สอย"].Number);
    }

    [Fact]
    public void LandAndBuilding_ReadsItsOwnHeaders()
    {
        using var file = PriceSheet.Build(PriceSheet.LbHeaders,
            ["12", "99/12", "Model-A", 50.25, 120, 3_000_000, 25_000, 2_900_000, 1_800_000, 400_000]);

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.LandAndBuilding);

        Assert.Empty(sheet.Errors);
        var row = Assert.Single(sheet.Rows);
        Assert.Equal("12", row.Key1);
        Assert.Equal("99/12", row.Key2);
        Assert.Equal(2_900_000m, row.AppraisalValue);
        Assert.Equal(50.25m, row.Attributes["เนื้อที่ดิน (ตร.วา)"].Number);
        Assert.Equal("Model-A", row.Attributes["แบบบ้าน"].Text);
    }

    [Fact]
    public void Headers_AreCaseAndWhitespaceInsensitive()
    {
        string[] headers = [" ห้อง ชุด ", "ราคาประเมินที่เสนอขออนุมัติ", "ราคาบังคับขาย (บาท)", "มูลค่าประกันอัคคีภัย"];
        using var file = PriceSheet.Build(headers, ["101", 10, 7, 1]);

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Empty(sheet.Errors);
        Assert.Equal(10m, Assert.Single(sheet.Rows).AppraisalValue);
    }

    [Fact]
    public void Numbers_AcceptCommaTextAndRoundToTwoDecimals()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: "1,408,945,000.00", std: 1408944999.9999998, force: 10.005, coverage: "2,500"));

        var row = Assert.Single(PriceSheet.Parse(file, UnitPriceImportLayout.Condo).Rows);

        Assert.Equal(1_408_945_000m, row.AppraisalValue);
        Assert.Equal(1_408_945_000m, row.StandardPrice);
        Assert.Equal(10.01m, row.ForceSellingPrice);
        Assert.Equal(2_500m, row.CoverageAmount);
    }

    [Fact]
    public void BlankRows_AreSkipped()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: 1),
            new object?[PriceSheet.CondoHeaders.Length],
            PriceSheet.CondoRow("102", appraisal: 2));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Equal([2, 4], sheet.Rows.Select(r => r.RowNumber));
        Assert.Null(sheet.TotalsRow);
    }

    [Fact]
    public void TrailingRowWithoutKeysButWithPrices_IsTheTotalsRow()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: 100, force: 70, coverage: 10),
            PriceSheet.CondoRow("102", appraisal: 200, force: 140, coverage: 20),
            PriceSheet.CondoRow(null, appraisal: 300, force: 210, coverage: 30));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Equal(2, sheet.Rows.Count);
        Assert.NotNull(sheet.TotalsRow);
        Assert.Equal(300m, sheet.TotalsRow!.AppraisalValue);
        Assert.Equal(210m, sheet.TotalsRow.ForceSellingPrice);
        Assert.Equal(30m, sheet.TotalsRow.CoverageAmount);
    }

    [Fact]
    public void KeylessRowInTheMiddle_IsNotATotalsRow()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: 100),
            PriceSheet.CondoRow(null, appraisal: 5),
            PriceSheet.CondoRow("102", appraisal: 200));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Equal(3, sheet.Rows.Count);
        Assert.Null(sheet.TotalsRow);
    }

    [Fact]
    public void MissingAppraisalHeader_IsBlocking()
    {
        using var file = PriceSheet.Build(["ห้องชุด", "ราคาบังคับขาย (บาท)", "มูลค่าประกันอัคคีภัย"], ["101", 5, 5]);

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        var error = Assert.Single(sheet.Errors);
        Assert.Equal(UnitPriceImportErrorCodes.MissingHeader, error.Code);
        Assert.Empty(sheet.Rows);
    }

    [Fact]
    public void MissingBothKeyHeaders_IsBlocking()
    {
        using var file = PriceSheet.Build(
            ["ราคาประเมินที่เสนอขออนุมัติ", "ราคาบังคับขาย (บาท)", "มูลค่าประกันอัคคีภัย"], [5, 4, 3]);

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Equal(UnitPriceImportErrorCodes.MissingHeader, Assert.Single(sheet.Errors).Code);
    }

    [Fact]
    public void OneKeyColumnAndOnlyTheAppraisalValue_IsEnough()
    {
        using var file = PriceSheet.Build(
            ["ห้องชุดเลขที่", "ราคาประเมินที่เสนอขออนุมัติ", "ราคาบังคับขาย (บาท)", "มูลค่าประกันอัคคีภัย"], ["9/101", 7, 5, 1]);

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Empty(sheet.Errors);
        var row = Assert.Single(sheet.Rows);
        Assert.Null(row.Key1);
        Assert.Equal("9/101", row.Key2);
        Assert.Null(row.StandardPrice);
        Assert.Equal(5m, row.ForceSellingPrice);
    }

    [Theory]
    [InlineData("ราคาบังคับขาย (บาท)")]
    [InlineData("มูลค่าประกันอัคคีภัย")]
    public void ForcedSaleAndInsuranceHeaders_AreRequired(string missing)
    {
        var headers = new[] { "ห้องชุด", "ราคาประเมินที่เสนอขออนุมัติ", "ราคาบังคับขาย (บาท)", "มูลค่าประกันอัคคีภัย" }
            .Where(h => h != missing).ToArray();
        using var file = PriceSheet.Build(headers, new object?[headers.Length]);

        var error = Assert.Single(PriceSheet.Parse(file, UnitPriceImportLayout.Condo).Errors);

        Assert.Equal(UnitPriceImportErrorCodes.MissingHeader, error.Code);
        Assert.Contains(missing, error.Message);
    }

    [Fact]
    public void TotalsRow_NeedsItsAppraisalValueToEqualTheRowsAbove()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: 100),
            PriceSheet.CondoRow("รวม", appraisal: 100.5),       // labelled and within 1 baht: totals
            PriceSheet.CondoRow(null, floor: 7));               // note under it: no price, ignored

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Single(sheet.Rows);
        Assert.Equal(100.5m, sheet.TotalsRow!.AppraisalValue);
    }

    [Fact]
    public void TowerSubtotalsAndAGrandTotal_AreAllLeftOut_AndTheGrandTotalIsTheSheetTotal()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: 100),
            PriceSheet.CondoRow("102", appraisal: 200),
            PriceSheet.CondoRow("รวม ตึก A", appraisal: 300),    // subtotal of tower A
            PriceSheet.CondoRow("201", appraisal: 50),
            PriceSheet.CondoRow("202", appraisal: 50),
            PriceSheet.CondoRow("รวม ตึก B", appraisal: 100),    // subtotal of tower B
            PriceSheet.CondoRow("รวมทั้งหมด", appraisal: 400),   // grand total
            PriceSheet.CondoRow(null));                         // blank line

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Equal(["101", "102", "201", "202"], sheet.Rows.Select(r => r.Key1));
        Assert.Equal(400m, sheet.TotalsRow!.AppraisalValue);
    }

    [Fact]
    public void SubtotalsWithoutAGrandTotal_AreLeftOut_ButAreNoSheetTotal()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: 100),
            PriceSheet.CondoRow("รวม", appraisal: 100),
            PriceSheet.CondoRow("201", appraisal: 50),
            PriceSheet.CondoRow("รวม", appraisal: 50));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Equal(2, sheet.Rows.Count);
        Assert.Null(sheet.TotalsRow);
    }

    [Fact]
    public void TotalsLabelInKey1_WithACountInKey2_IsStillTheTotalsRow()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: 100),
            PriceSheet.CondoRow("รวม", "120 ห้อง", appraisal: 100, force: 70));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Single(sheet.Rows);
        Assert.Equal(70m, sheet.TotalsRow!.ForceSellingPrice);
    }

    [Fact]
    public void FooterNotesUnderTheTotal_AreIgnored_ButAFooterWithAStrayPriceIsKept()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: 100),
            PriceSheet.CondoRow("total", appraisal: 100),
            PriceSheet.CondoRow(null, tower: "Prepared by ..."),
            PriceSheet.CondoRow(null, appraisal: 55));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Equal([2, 5], sheet.Rows.Select(r => r.RowNumber));
        Assert.NotNull(sheet.TotalsRow);
    }

    [Fact]
    public void LabelledRowThatDoesNotAddUp_IsNotATotalsRow()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: 100),
            PriceSheet.CondoRow("Total", appraisal: 500));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Null(sheet.TotalsRow);
        Assert.Equal(2, sheet.Rows.Count);
    }

    [Fact]
    public void UnreadableNumber_IsReportedWithRowAndKey()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: "abc"));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        var error = Assert.Single(sheet.Errors);
        Assert.Equal(UnitPriceImportErrorCodes.InvalidNumber, error.Code);
        Assert.Equal(2, error.RowNumber);
        Assert.Equal("101", error.Key);
    }

    [Fact]
    public void NegativePrice_IsInvalid_ButZeroIsAllowed()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: -5),
            PriceSheet.CondoRow("102", appraisal: 0, force: "-1,000", coverage: 0));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Equal([2, 3], sheet.Errors.Select(e => e.RowNumber));
        Assert.All(sheet.Errors, e =>
        {
            Assert.Equal(UnitPriceImportErrorCodes.InvalidNumber, e.Code);
            Assert.Contains("negative", e.Message);
        });
        Assert.Equal(0m, sheet.Rows[1].AppraisalValue);
        Assert.Equal(0m, sheet.Rows[1].CoverageAmount);
    }

    [Fact]
    public void NumberErrors_AreRaisedOnlyForDataRows()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: 100),
            PriceSheet.CondoRow("รวม", appraisal: 100),
            new object?[] { null, null, null, null, null, null, null, null, "see note 3", null, null },   // footer note
            PriceSheet.CondoRow("102", appraisal: "oops"));        // a data row: still an error

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        var error = Assert.Single(sheet.Errors);
        Assert.Equal(UnitPriceImportErrorCodes.InvalidNumber, error.Code);
        Assert.Equal(5, error.RowNumber);
        Assert.Equal(2, sheet.Rows.Count);   // 101 and 102; the note is gone
    }

    [Fact]
    public void KeylessRowWithAParseablePriceThatMatchesNoSum_StaysAndItsNumberErrorsAreKept()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: 100),
            PriceSheet.CondoRow(null, appraisal: 7, force: "bad"));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Equal(2, sheet.Rows.Count);
        Assert.Equal(UnitPriceImportErrorCodes.InvalidNumber, Assert.Single(sheet.Errors).Code);
    }

    [Theory]
    [InlineData("9999999999999999")]          // text, 1e16
    [InlineData("100,000,000,000")]           // text, exactly 1e11 (100 billion)
    [InlineData(1e11)]                        // numeric cell, exactly 1e11
    [InlineData(1e16)]
    [InlineData(1e300)]
    public void NumbersBeyondTheStorableRange_AreInvalid_NotAFailedSave(object tooBig)
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders, PriceSheet.CondoRow("101", appraisal: tooBig));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Equal(UnitPriceImportErrorCodes.InvalidNumber, Assert.Single(sheet.Errors).Code);
    }

    [Fact]
    public void JustUnderTheLimit_IsFine()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders, PriceSheet.CondoRow("101", appraisal: "99,999,999,999.99"));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Empty(sheet.Errors);
        Assert.Equal(99_999_999_999.99m, sheet.Rows[0].AppraisalValue);
    }

    [Theory]
    [InlineData("-")]
    [InlineData(" \u2013 ")]   // en dash
    [InlineData("\u2014")]     // em dash
    public void ADashAloneInAPriceCell_MeansBlank_NotInvalid(string dash)
    {
        // Appraisal value given, forced sale and insurance dashed out.
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: 100, force: dash, coverage: dash),
            PriceSheet.CondoRow("102", appraisal: dash, force: dash, coverage: dash));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Empty(sheet.Errors);
        Assert.Null(sheet.Rows[0].ForceSellingPrice);
        Assert.Null(sheet.Rows[0].CoverageAmount);
        Assert.False(sheet.Rows[1].HasPrice);   // all dashes = a unit without a price
    }

    [Fact]
    public void ADashInsideANumber_IsStillInvalid()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders, PriceSheet.CondoRow("101", appraisal: "12-34"));

        Assert.Equal(UnitPriceImportErrorCodes.InvalidNumber, Assert.Single(PriceSheet.Parse(file, UnitPriceImportLayout.Condo).Errors).Code);
    }

    [Theory]
    [InlineData("ยอดรวม")]
    [InlineData("Grand Total")]
    [InlineData("Sub total")]
    [InlineData("รวมทั้งสิ้น")]
    public void AKeyCellContainingTheLabel_MakesASummaryRow(string label)
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: 100),
            PriceSheet.CondoRow(label, appraisal: 100));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Single(sheet.Rows);
        Assert.Equal(100m, sheet.TotalsRow!.AppraisalValue);
    }

    [Fact]
    public void ALabelledRowThatAddsUpToNothing_IsNotASummaryRow()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: 100),
            PriceSheet.CondoRow("Totally-new-room", appraisal: 5));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Equal(2, sheet.Rows.Count);
        Assert.Null(sheet.TotalsRow);
    }

    [Fact]
    public void DashOnlyKeyCells_AreBlank()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("-", " \u2014 ", appraisal: 100),
            PriceSheet.CondoRow("102", "-", appraisal: 200));

        var rows = PriceSheet.Parse(file, UnitPriceImportLayout.Condo).Rows;

        Assert.Null(rows[0].DisplayKey);
        Assert.Equal("102", rows[1].Key1);
        Assert.Null(rows[1].Key2);
    }

    [Fact]
    public void NotAWorkbook_Throws()
    {
        using var file = new MemoryStream("not an xlsx"u8.ToArray());

        Assert.Throws<BadRequestException>(() => PriceSheet.Parse(file, UnitPriceImportLayout.Condo));
    }

    [Fact]
    public void TenThousandDataRows_AreAccepted_AndOneMoreIsRejected()
    {
        object?[][] Rows(int n) => Enumerable.Range(1, n).Select(i => PriceSheet.CondoRow(i.ToString(), appraisal: 1)).ToArray();

        using var atLimit = PriceSheet.Build(PriceSheet.CondoHeaders, Rows(UnitPriceExcelParser.MaxRows));
        Assert.Equal(UnitPriceExcelParser.MaxRows, PriceSheet.Parse(atLimit, UnitPriceImportLayout.Condo).Rows.Count);

        using var overLimit = PriceSheet.Build(PriceSheet.CondoHeaders, Rows(UnitPriceExcelParser.MaxRows + 1));
        Assert.Throws<BadRequestException>(() => PriceSheet.Parse(overLimit, UnitPriceImportLayout.Condo));
    }

    [Fact]
    public void OnlyTheFirstWorksheetIsRead()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders, PriceSheet.CondoRow("101", appraisal: 1));
        using var wb = new ClosedXML.Excel.XLWorkbook(file);
        var second = wb.Worksheets.Add("Other");
        second.Cell(1, 1).Value = "ห้องชุด";
        second.Cell(1, 2).Value = "ราคาประเมินที่เสนอขออนุมัติ";
        second.Cell(2, 1).Value = "999";
        second.Cell(2, 2).Value = 9;
        using var saved = new MemoryStream();
        wb.SaveAs(saved);
        saved.Position = 0;

        var sheet = PriceSheet.Parse(saved, UnitPriceImportLayout.Condo);

        Assert.Equal("101", Assert.Single(sheet.Rows).Key1);
    }

    [Fact]
    public void NumericKeyCells_AndPaddedTextKeys_ReadAsTrimmedText()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow(101, "  9/101  ", appraisal: 1));

        var row = Assert.Single(PriceSheet.Parse(file, UnitPriceImportLayout.Condo).Rows);

        Assert.Equal("101", row.Key1);
        Assert.Equal("9/101", row.Key2);
    }

    [Fact]
    public void KeyedRowWithNoPrices_AtTheEnd_IsADataRowNotATotalsRow()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: 1),
            PriceSheet.CondoRow("102"));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.Condo);

        Assert.Equal(2, sheet.Rows.Count);
        Assert.Null(sheet.TotalsRow);
    }

    [Fact]
    public void CommaTextNumbers_Parse_AndTrailingDotZeroIsFine()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders,
            PriceSheet.CondoRow("101", appraisal: "1,234.5", force: "5", coverage: "7.0"));

        var row = Assert.Single(PriceSheet.Parse(file, UnitPriceImportLayout.Condo).Rows);

        Assert.Equal(1234.5m, row.AppraisalValue);
        Assert.Equal(5m, row.ForceSellingPrice);
        Assert.Equal(7m, row.CoverageAmount);
    }

    [Fact]
    public void LandAndBuilding_RejectsACondoHeaderSet()
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders, PriceSheet.CondoRow("101", appraisal: 1));

        var sheet = PriceSheet.Parse(file, UnitPriceImportLayout.LandAndBuilding);

        Assert.All(sheet.Errors, e => Assert.Equal(UnitPriceImportErrorCodes.MissingHeader, e.Code));
        Assert.NotEmpty(sheet.Errors);
    }
}
