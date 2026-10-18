using Appraisal.Application.Features.Project.ImportProjectUnitPrices;
using Appraisal.Domain.Projects;
using Shared.Exceptions;

namespace Appraisal.Tests.Application.Features.ImportProjectUnitPrices;

public class UnitPriceImportAnalyzerTests
{
    private static UnitPriceImportAnalysis Run(object?[][] rows, params ProjectUnit[] units)
    {
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders, rows);
        return PriceSheet.Analyze(PriceSheet.Parse(file, UnitPriceImportLayout.Condo), units);
    }

    [Fact]
    public void MatchesByPrimaryKey()
    {
        var a = PriceSheet.CondoUnit(1, "101", "9/101");

        var result = Run([PriceSheet.CondoRow("101", appraisal: 100, force: 70, coverage: 10)], a);

        var r = result.Response;
        Assert.Empty(r.Errors);
        Assert.True(r.CanImport);
        Assert.Equal(1, r.MatchedCount);
        Assert.Equal(1, r.MatchedByPrimaryKey);
        Assert.Equal(0, r.MatchedBySecondaryKey);
        Assert.Equal(a.Id, Assert.Single(r.Rows).ProjectUnitId);
        Assert.Equal(new UnitPriceImportTotals(100, 70, 10), r.Totals);
    }

    [Fact]
    public void FallsBackToSecondaryKey_WhenPrimaryDoesNotMatch()
    {
        var a = PriceSheet.CondoUnit(1, "101", "9/101");

        var result = Run([PriceSheet.CondoRow("999", "9/101", appraisal: 100)], a);

        Assert.Empty(result.Response.Errors);
        Assert.Equal(1, result.Response.MatchedBySecondaryKey);
        Assert.Equal(0, result.Response.MatchedByPrimaryKey);
        Assert.Contains(a.Id, result.Matched.Keys);
    }

    [Fact]
    public void BothKeysOnTheSameUnit_IsFine()
    {
        var a = PriceSheet.CondoUnit(1, "101", "9/101");

        var result = Run([PriceSheet.CondoRow("101", "9/101", appraisal: 100)], a);

        Assert.Empty(result.Response.Errors);
        Assert.Equal(1, result.Response.MatchedByPrimaryKey);
    }

    [Fact]
    public void KeysPointingAtDifferentUnits_IsAmbiguous()
    {
        var a = PriceSheet.CondoUnit(1, "101", "9/101");
        var b = PriceSheet.CondoUnit(2, "102", "9/102");

        var result = Run([PriceSheet.CondoRow("101", "9/102", appraisal: 100)], a, b);

        var error = Assert.Single(result.Response.Errors);
        Assert.Equal(UnitPriceImportErrorCodes.AmbiguousMatch, error.Code);
        Assert.False(result.Response.CanImport);
        Assert.Empty(result.Matched);
    }

    [Fact]
    public void KeyMatchingTwoUnits_IsAmbiguous()
    {
        var a = PriceSheet.CondoUnit(1, "101");
        var b = PriceSheet.CondoUnit(2, "101");

        var result = Run([PriceSheet.CondoRow("101", appraisal: 100)], a, b);

        Assert.Equal(UnitPriceImportErrorCodes.AmbiguousMatch, Assert.Single(result.Response.Errors).Code);
    }

    [Fact]
    public void SharedPrimaryKey_IsDisambiguatedBySecondaryKey()
    {
        var a = PriceSheet.CondoUnit(1, "101", "9/101");
        var b = PriceSheet.CondoUnit(2, "101", "9/201");

        var result = Run([PriceSheet.CondoRow("101", "9/201", appraisal: 100)], a, b);

        Assert.Empty(result.Response.Errors);
        Assert.Equal(b.Id, Assert.Single(result.Matched).Key);
        Assert.Equal(1, result.Response.MatchedByPrimaryKey);
    }

    [Fact]
    public void SharedPrimaryKey_StaysAmbiguous_WhenSecondaryKeyIsEmptyUnknownOrStillShared()
    {
        var a = PriceSheet.CondoUnit(1, "101", "9/101");
        var b = PriceSheet.CondoUnit(2, "101", "9/201");
        var c = PriceSheet.CondoUnit(3, "101", "9/201");

        var result = Run(
            [
                PriceSheet.CondoRow("101", null, appraisal: 1),     // key 2 empty
                PriceSheet.CondoRow("101", "9/999", appraisal: 2),  // key 2 matches no unit
                PriceSheet.CondoRow("101", "9/201", appraisal: 3)   // key 2 still leaves two units
            ], a, b, c);

        Assert.Equal(3, result.Response.Errors.Count);
        Assert.All(result.Response.Errors, e => Assert.Equal(UnitPriceImportErrorCodes.AmbiguousMatch, e.Code));
        Assert.Empty(result.Matched);
    }

    [Fact]
    public void TwoRowsOnOneUnit_IsDuplicate_AndTheLaterRowIsFlagged()
    {
        var a = PriceSheet.CondoUnit(1, "101", "9/101");

        var result = Run(
            [PriceSheet.CondoRow("101", appraisal: 100), PriceSheet.CondoRow(null, "9/101", appraisal: 200)], a);

        var error = Assert.Single(result.Response.Errors);
        Assert.Equal(UnitPriceImportErrorCodes.DuplicateUnit, error.Code);
        Assert.Equal(3, error.RowNumber);
        Assert.Equal(1, result.Response.MatchedCount);
        Assert.Equal(100m, result.Matched[a.Id].AppraisalValue);
    }

    [Fact]
    public void RowWithoutAUnit_IsNotFound()
    {
        var a = PriceSheet.CondoUnit(1, "101");

        var result = Run([PriceSheet.CondoRow("777", appraisal: 100)], a);

        var error = Assert.Single(result.Response.Errors);
        Assert.Equal(UnitPriceImportErrorCodes.UnitNotFound, error.Code);
        Assert.Equal("777", error.Key);
        Assert.Null(Assert.Single(result.Response.Rows).ProjectUnitId);
        Assert.False(result.Response.CanImport);
    }

    [Fact]
    public void KeylessRowWithAPriceThatIsNoSubtotal_IsNotFound_ButOneWithoutPricesIsIgnored()
    {
        var a = PriceSheet.CondoUnit(1, "101");

        var result = Run(
            [
                PriceSheet.CondoRow("101", appraisal: 100),
                PriceSheet.CondoRow(null, floor: 4, appraisal: null),   // heading / note: ignored
                PriceSheet.CondoRow(null, appraisal: 7),                // adds up to nothing: blocking
                PriceSheet.CondoRow("102", appraisal: 1)
            ], a);

        Assert.Contains(result.Response.Errors, e => e.Code == UnitPriceImportErrorCodes.UnitNotFound && e.RowNumber == 4);
        Assert.DoesNotContain(result.Response.Errors, e => e.RowNumber == 3);
    }

    [Fact]
    public void MatchedRowWithoutAppraisalValue_IsBlocking()
    {
        var a = PriceSheet.CondoUnit(1, "101");

        var result = Run([PriceSheet.CondoRow("101", force: 5)], a);

        Assert.Equal(UnitPriceImportErrorCodes.MissingAppraisalValue, Assert.Single(result.Response.Errors).Code);
        Assert.False(result.Response.CanImport);
    }

    [Fact]
    public void MatchedRowWithAllPricesBlank_IsAUnitWithoutPrice_NotAnError()
    {
        var a = PriceSheet.CondoUnit(1, "101");
        var b = PriceSheet.CondoUnit(2, "102");

        var result = Run([PriceSheet.CondoRow("101", appraisal: 100), PriceSheet.CondoRow("102")], a, b);

        var r = result.Response;
        Assert.Empty(r.Errors);
        Assert.True(r.CanImport);
        Assert.Equal(1, r.MatchedCount);
        Assert.Equal([b.Id], result.MissingUnitIds);
        Assert.Equal(b.Id, Assert.Single(r.UnitsMissingFromFile).ProjectUnitId);
        Assert.Equal(2, r.RowCount);
        Assert.Equal(b.Id, r.Rows[1].ProjectUnitId);
        Assert.Null(r.Rows[1].NewAppraisalValue);
    }

    [Fact]
    public void FileWhereEveryPriceIsBlank_CannotImport()
    {
        var result = Run([PriceSheet.CondoRow("101")], PriceSheet.CondoUnit(1, "101"));

        Assert.Empty(result.Response.Errors);
        Assert.False(result.Response.CanImport);
    }

    [Fact]
    public void TwoBlankPriceRowsOnOneUnit_AreStillDuplicates()
    {
        var a = PriceSheet.CondoUnit(1, "101", "9/101");

        var result = Run([PriceSheet.CondoRow("101"), PriceSheet.CondoRow("101")], a);

        Assert.Equal(UnitPriceImportErrorCodes.DuplicateUnit, Assert.Single(result.Response.Errors).Code);
    }

    [Fact]
    public void KeysMatchIgnoringCaseAndSurroundingSpaces()
    {
        var a = PriceSheet.CondoUnit(1, "05A04", "9/05A04");
        var b = PriceSheet.CondoUnit(2, "07B01", "9/07B01");

        var result = Run(
            [PriceSheet.CondoRow(" 05a04 ", appraisal: 100), PriceSheet.CondoRow("zz", "9/07b01", appraisal: 200)], a, b);

        Assert.Empty(result.Response.Errors);
        Assert.Equal(2, result.Response.MatchedCount);
        Assert.Equal(1, result.Response.MatchedByPrimaryKey);
        Assert.Equal(1, result.Response.MatchedBySecondaryKey);
    }

    [Fact]
    public void UnitsWithoutARow_AreListedAsMissing_NotBlocking()
    {
        var a = PriceSheet.CondoUnit(1, "101");
        var b = PriceSheet.CondoUnit(2, "102", "9/102");
        var c = PriceSheet.CondoUnit(3, null, null);

        var result = Run([PriceSheet.CondoRow("101", appraisal: 100)], a, b, c);

        Assert.Empty(result.Response.Errors);
        Assert.True(result.Response.CanImport);
        Assert.Equal(3, result.Response.UnitsInProject);
        Assert.Equal([b.Id, c.Id], result.MissingUnitIds);
        Assert.Equal("102", result.Response.UnitsMissingFromFile[0].Key);
        Assert.Null(result.Response.UnitsMissingFromFile[1].Key);
    }

    [Fact]
    public void NoMatchedRow_CannotImport()
    {
        var result = Run([], PriceSheet.CondoUnit(1, "101"));

        Assert.False(result.Response.CanImport);
        Assert.Equal(0, result.Response.MatchedCount);
    }

    [Fact]
    public void ReportsPreviousValueFromStoredPrices()
    {
        var a = PriceSheet.CondoUnit(1, "101");
        using var file = PriceSheet.Build(PriceSheet.CondoHeaders, PriceSheet.CondoRow("101", appraisal: 150));

        var result = PriceSheet.Analyze(
            PriceSheet.Parse(file, UnitPriceImportLayout.Condo), [a], new Dictionary<Guid, decimal?> { [a.Id] = 120m });

        var row = Assert.Single(result.Response.Rows);
        Assert.Equal(120m, row.PreviousAppraisalValue);
        Assert.Equal(150m, row.NewAppraisalValue);
    }

    [Fact]
    public void CompareOnlyColumns_AreReportedWhenTheyDiffer_AndNeverBlock()
    {
        var a = PriceSheet.CondoUnit(1, "101", floor: 3, tower: "A", model: "T1", area: 35.5m, selling: 2_000_000m);

        // floor, tower and usable area differ; model and selling price are the same (float noise included)
        var result = Run(
            [PriceSheet.CondoRow("101", appraisal: 100, floor: 5, tower: "B", model: "T1", area: 36, selling: 2_000_000.0000001)], a);

        Assert.Empty(result.Response.Errors);
        Assert.Equal(
            ["ชั้นที่", "อาคาร", "พื้นที่ใช้สอย"],
            result.Response.AttributeDiffs.Select(d => d.Column));
        var floor = result.Response.AttributeDiffs[0];
        Assert.Equal("3", floor.SystemValue);
        Assert.Equal("5", floor.FileValue);
        Assert.Equal("101", floor.Key);
        Assert.Equal(2, floor.RowNumber);
    }

    [Fact]
    public void BlankCompareCell_AgainstAStoredValue_IsADifference()
    {
        var a = PriceSheet.CondoUnit(1, "101", tower: "A");

        var result = Run([PriceSheet.CondoRow("101", appraisal: 100)], a);

        var diff = Assert.Single(result.Response.AttributeDiffs);
        Assert.Equal("อาคาร", diff.Column);
        Assert.Equal("A", diff.SystemValue);
        Assert.Null(diff.FileValue);
    }

    [Fact]
    public void SheetTotals_MatchWithinOneBaht()
    {
        var a = PriceSheet.CondoUnit(1, "101");
        var b = PriceSheet.CondoUnit(2, "102");

        var result = Run(
            [
                PriceSheet.CondoRow("101", appraisal: 100, force: 70),
                PriceSheet.CondoRow("102", appraisal: 200, force: 140),
                PriceSheet.CondoRow(null, appraisal: 300.5, force: 210)
            ], a, b);

        Assert.Equal(2, result.Response.RowCount);
        Assert.True(result.Response.SheetTotalsMatch);
        Assert.Equal(300.5m, result.Response.SheetTotals!.AppraisalValue);
        Assert.Equal(0m, result.Response.SheetTotals.CoverageAmount);
    }

    [Fact]
    public void SheetTotals_OtherColumnMismatch_IsReported()
    {
        var a = PriceSheet.CondoUnit(1, "101");

        var result = Run(
            [PriceSheet.CondoRow("101", appraisal: 100, force: 70), PriceSheet.CondoRow(null, appraisal: 100, force: 99)], a);

        Assert.Empty(result.Response.Errors);
        Assert.False(result.Response.SheetTotalsMatch);
    }

    [Fact]
    public void KeylessRowThatIsNotTheSumOfTheRowsAbove_IsABlockingError()
    {
        var a = PriceSheet.CondoUnit(1, "101");

        var result = Run(
            [PriceSheet.CondoRow("101", appraisal: 100), PriceSheet.CondoRow(null, appraisal: 105)], a);

        var error = Assert.Single(result.Response.Errors);
        Assert.Equal(UnitPriceImportErrorCodes.UnitNotFound, error.Code);
        Assert.Equal(3, error.RowNumber);
        Assert.Null(result.Response.SheetTotals);
        Assert.False(result.Response.CanImport);
    }

    [Fact]
    public void MatchedPricedRowWithBlankForcedSale_IsMissingPrice_ButBlankInsuranceIsAllowed()
    {
        var a = PriceSheet.CondoUnit(1, "101");
        var b = PriceSheet.CondoUnit(2, "102");
        // room, regNo, floor, tower, model, area, selling, std, appraisal, force, coverage
        object?[] noForce = ["101", null, null, null, null, null, null, null, 100, null, 5];
        object?[] noCoverage = ["102", null, null, null, null, null, null, null, 200, 140, null];   // fine

        var result = Run([noForce, noCoverage], a, b);

        var error = Assert.Single(result.Response.Errors);
        Assert.Equal(UnitPriceImportErrorCodes.MissingPrice, error.Code);
        Assert.Equal(2, error.RowNumber);
        Assert.Contains(UnitPriceImportLayout.Condo.ForceSellingPriceHeader, error.Message);
        Assert.False(result.Response.CanImport);
    }

    [Fact]
    public void SecondaryKeyThatDisagreesWithTheUnitFoundByThePrimaryKey_IsAnInfoDiff()
    {
        var a = PriceSheet.CondoUnit(1, "101", "9/101");

        var result = Run(
            [PriceSheet.CondoRow("101", "9/999", appraisal: 100), PriceSheet.CondoRow("101", " 9/101 ", appraisal: 1)], a);

        // Row 3 repeats the unit (duplicate error), but its equal key 2 adds no diff; row 2 disagrees.
        var diff = Assert.Single(result.Response.AttributeDiffs);
        Assert.Equal(UnitPriceImportLayout.Condo.Key2Header, diff.Column);
        Assert.Equal("9/101", diff.SystemValue);
        Assert.Equal("9/999", diff.FileValue);
        Assert.Equal(2, diff.RowNumber);
    }

    [Fact]
    public void LabelledTotalsRow_IsRecognised_AndFootersUnderItAreIgnored()
    {
        var a = PriceSheet.CondoUnit(1, "101");
        var b = PriceSheet.CondoUnit(2, "102");

        var result = Run(
            [
                PriceSheet.CondoRow("101", appraisal: 100),
                PriceSheet.CondoRow("102", appraisal: 200),
                PriceSheet.CondoRow(" รวม ", appraisal: 300),
                PriceSheet.CondoRow(null, appraisal: null, floor: 7),      // note under the totals
                new object?[PriceSheet.CondoHeaders.Length]                // blank line
            ], a, b);

        Assert.Empty(result.Response.Errors);
        Assert.Equal(2, result.Response.RowCount);
        Assert.Equal(300m, result.Response.SheetTotals!.AppraisalValue);
        Assert.True(result.Response.CanImport);
    }

    [Fact]
    public void KeyedRowAfterTheTotalsRow_IsStillData()
    {
        var a = PriceSheet.CondoUnit(1, "101");
        var b = PriceSheet.CondoUnit(2, "102");

        var result = Run(
            [
                PriceSheet.CondoRow("101", appraisal: 100),
                PriceSheet.CondoRow("TOTAL", appraisal: 100),
                PriceSheet.CondoRow("102", appraisal: 200)
            ], a, b);

        Assert.Empty(result.Response.Errors);
        Assert.Equal(2, result.Response.RowCount);
        Assert.Equal(2, result.Response.MatchedCount);
    }

    [Fact]
    public void NoTotalsRow_MeansNullTotalsAndNullMatch()
    {
        var result = Run([PriceSheet.CondoRow("101", appraisal: 100)], PriceSheet.CondoUnit(1, "101"));

        Assert.Null(result.Response.SheetTotals);
        Assert.Null(result.Response.SheetTotalsMatch);
    }

    [Fact]
    public void Lb_MatchesPlotThenHouse()
    {
        var u1 = PriceSheet.LbUnit(1, "12", "99/12", land: 50.25m);
        var u2 = PriceSheet.LbUnit(2, "13", "99/13");
        using var file = PriceSheet.Build(PriceSheet.LbHeaders,
            ["12", null, null, 50.25, null, null, null, 1000, 0, 0],
            ["zz", "99/13", null, null, null, null, null, 2000, 0, 0]);

        var result = PriceSheet.AnalyzeWith(
            "lb.xlsx", PriceSheet.Parse(file, UnitPriceImportLayout.LandAndBuilding),
            UnitPriceImportLayout.LandAndBuilding, [u1, u2], new Dictionary<Guid, decimal?>());

        Assert.Empty(result.Response.Errors);
        Assert.Equal("LandAndBuilding", result.Response.ProjectType);
        Assert.Equal(1, result.Response.MatchedByPrimaryKey);
        Assert.Equal(1, result.Response.MatchedBySecondaryKey);
        Assert.Equal(3000m, result.Response.Totals.AppraisalValue);
    }

    [Fact]
    public void KeysAreTrimmedOnBothSides_BeforeMatching()
    {
        var a = PriceSheet.CondoUnit(1, " 101 ", " 9/101");

        var byKey1 = Run([PriceSheet.CondoRow("101", appraisal: 1)], a);
        var byKey2 = Run([PriceSheet.CondoRow("zz", "9/101 ", appraisal: 1)], a);

        Assert.Empty(byKey1.Response.Errors);
        Assert.Empty(byKey2.Response.Errors);
        Assert.Equal(1, byKey2.Response.MatchedBySecondaryKey);
    }

    [Fact]
    public void KeyMatching_IsCaseInsensitive()
    {
        var a = PriceSheet.CondoUnit(1, "15a01");

        var result = Run([PriceSheet.CondoRow("15A01", appraisal: 1)], a);

        Assert.Empty(result.Response.Errors);
        Assert.Contains(a.Id, result.Matched.Keys);
    }

    [Fact]
    public void ErrorsAreReturnedInRowOrder_AndEveryBadRowIsReported()
    {
        var a = PriceSheet.CondoUnit(1, "101");

        var result = Run(
            [
                PriceSheet.CondoRow("900", appraisal: 1),
                PriceSheet.CondoRow("101", appraisal: 1),
                PriceSheet.CondoRow("101", appraisal: 2),
                PriceSheet.CondoRow("901", appraisal: 3)
            ], a);

        Assert.Equal([2, 4, 5], result.Response.Errors.Select(e => e.RowNumber));
        Assert.Equal(
            [UnitPriceImportErrorCodes.UnitNotFound, UnitPriceImportErrorCodes.DuplicateUnit, UnitPriceImportErrorCodes.UnitNotFound],
            result.Response.Errors.Select(e => e.Code));
    }

    [Fact]
    public void UnparsedNumberError_IsKept_AndBlocksTheImport()
    {
        var a = PriceSheet.CondoUnit(1, "101");

        var result = Run([PriceSheet.CondoRow("101", appraisal: "abc")], a);

        Assert.Contains(result.Response.Errors, e => e.Code == UnitPriceImportErrorCodes.InvalidNumber);
        Assert.False(result.Response.CanImport);
    }

    [Fact]
    public void Totals_SumEveryMatchedRow_IncludingUnitsThatAreSold()
    {
        var sold = PriceSheet.CondoUnit(1, "101");
        sold.MarkSoldByReappraisal();
        var open = PriceSheet.CondoUnit(2, "102");

        var result = Run(
            [PriceSheet.CondoRow("101", appraisal: 100, force: 80, coverage: 10), PriceSheet.CondoRow("102", appraisal: 200, force: 160, coverage: 20)],
            sold, open);

        Assert.Equal(new UnitPriceImportTotals(300, 240, 30), result.Response.Totals);
        Assert.Equal(2, result.Matched.Count);
    }

    [Fact]
    public void SheetTotals_MismatchOnForcedSaleOrCoverageAlone_IsReported()
    {
        var a = PriceSheet.CondoUnit(1, "101");

        var forced = Run(
            [PriceSheet.CondoRow("101", appraisal: 100, force: 70), PriceSheet.CondoRow(null, appraisal: 100, force: 90)], a);
        var coverage = Run(
            [PriceSheet.CondoRow("101", appraisal: 100, coverage: 10), PriceSheet.CondoRow(null, appraisal: 100, coverage: 12)], a);

        Assert.False(forced.Response.SheetTotalsMatch);
        Assert.False(coverage.Response.SheetTotalsMatch);
    }

    [Fact]
    public void UnknownPrimaryKey_WithASecondaryKeyThatMatches_MatchesAndReportsKey1AsADifference()
    {
        var a = PriceSheet.CondoUnit(1, "101", "9/101");

        var result = Run([PriceSheet.CondoRow("999", "9/101", appraisal: 100)], a);

        Assert.Empty(result.Response.Errors);
        Assert.Equal(1, result.Response.MatchedBySecondaryKey);
        var diff = Assert.Single(result.Response.AttributeDiffs);
        Assert.Equal(UnitPriceImportLayout.Condo.Key1Header, diff.Column);
        Assert.Equal("101", diff.SystemValue);
        Assert.Equal("999", diff.FileValue);
    }

    [Fact]
    public void PrimaryKeyMatchesOneUnit_AndSecondaryKeyNamesOnlyOtherUnits_IsAmbiguous_EvenWithSeveralHits()
    {
        var a = PriceSheet.CondoUnit(1, "101", "9/101");
        var b = PriceSheet.CondoUnit(2, "102", "9/X");
        var c = PriceSheet.CondoUnit(3, "103", "9/X");

        var result = Run(
            [
                PriceSheet.CondoRow("101", "9/X", appraisal: 100),   // two other units share key 2
                PriceSheet.CondoRow("102", "9/102", appraisal: 1)    // key 2 matches nothing: a difference only
            ], a, b, c);

        var error = Assert.Single(result.Response.Errors);
        Assert.Equal(UnitPriceImportErrorCodes.AmbiguousMatch, error.Code);
        Assert.Equal(2, error.RowNumber);
        Assert.Contains(result.Response.AttributeDiffs, d => d.RowNumber == 3 && d.Column == UnitPriceImportLayout.Condo.Key2Header);
    }

    [Fact]
    public void PrimaryKeyUnitAmongTheSecondaryKeyHits_IsFine()
    {
        var a = PriceSheet.CondoUnit(1, "101", "9/X");
        var b = PriceSheet.CondoUnit(2, "102", "9/X");

        var result = Run([PriceSheet.CondoRow("101", "9/X", appraisal: 100)], a, b);

        Assert.Empty(result.Response.Errors);
        Assert.Empty(result.Response.AttributeDiffs);
        Assert.Contains(a.Id, result.Matched.Keys);
    }

    [Fact]
    public void SharedKey_InTwoTowers_IsResolvedByTheTowerColumn_AndStaysAmbiguousWithoutOne()
    {
        var a = PriceSheet.CondoUnit(1, "101", null, 1, "A");
        var b = PriceSheet.CondoUnit(2, "101", null, 1, "B");

        var result = Run(
            [
                PriceSheet.CondoRow("101", tower: " b ", appraisal: 200),   // tower picks B
                PriceSheet.CondoRow("101", tower: "C", appraisal: 1),       // no such tower
                PriceSheet.CondoRow("101", appraisal: 1)                    // no tower at all
            ], a, b);

        Assert.Equal([b.Id], result.Matched.Keys);
        Assert.Equal([3, 4], result.Response.Errors.Select(e => e.RowNumber));
        Assert.All(result.Response.Errors, e => Assert.Equal(UnitPriceImportErrorCodes.AmbiguousMatch, e.Code));
    }

    [Fact]
    public void TowerBreaksTheTieWhenKey2IsUnknown_ButNotWhenKey2AlreadyDecided()
    {
        var a = PriceSheet.CondoUnit(1, "101", "9/1", 1, "A");
        var b = PriceSheet.CondoUnit(2, "101", "9/2", 1, "B");

        var result = Run(
            [
                PriceSheet.CondoRow("101", "9/ZZ", tower: "B", appraisal: 5),  // key 2 matches nothing -> tower
                PriceSheet.CondoRow("101", "9/1", tower: "B", appraisal: 7)    // key 2 names A: key 2 wins
            ], a, b);

        Assert.Empty(result.Response.Errors);
        Assert.Equal(5m, result.Matched[b.Id].AppraisalValue);
        Assert.Equal(7m, result.Matched[a.Id].AppraisalValue);
    }

    [Fact]
    public void NoPriceRowWhoseKeyMatchesNoUnit_IsIgnored_ButOneThatMatchesIsAUnitWithoutAPrice()
    {
        var a = PriceSheet.CondoUnit(1, "101");
        var b = PriceSheet.CondoUnit(2, "102");
        object?[] note(object? key) => [key, null, null, null, null, null, null, null, null, null, null];

        var result = Run(
            [
                PriceSheet.CondoRow("101", appraisal: 100),
                note("ห้องชุด"),            // a repeated header typed in the key column
                note("Prepared by Somchai"),
                note("102")                  // matches a unit: that unit simply has no price
            ], a, b);

        Assert.Empty(result.Response.Errors);
        Assert.Equal(2, result.Response.RowCount);   // the two ignored notes are not rows
        Assert.Equal(1, result.Response.MatchedCount);
        Assert.Equal([b.Id], result.MissingUnitIds);
    }

    [Fact]
    public void PricedRowWhoseKeyMatchesNoUnit_IsStillNotFound()
    {
        var result = Run([PriceSheet.CondoRow("777", appraisal: 5)], PriceSheet.CondoUnit(1, "101"));

        Assert.Equal(UnitPriceImportErrorCodes.UnitNotFound, Assert.Single(result.Response.Errors).Code);
    }

    [Fact]
    public void DashOnlyKeysAreNoKeys_InTheFileAndOnTheUnits()
    {
        // The unit's stored registration number is "-": it has no key 2 to match, and its key 1 still works.
        var a = PriceSheet.CondoUnit(1, "101", "-");
        var b = PriceSheet.CondoUnit(2, "102", "9/102");

        var result = Run(
            [
                PriceSheet.CondoRow("101", "-", appraisal: 100),          // file key 2 "-" is blank, not a mismatch
                PriceSheet.CondoRow("\u2013", "9/102", appraisal: 200)   // key 1 is an en dash: matched by key 2
            ], a, b);

        Assert.Empty(result.Response.Errors);
        Assert.Empty(result.Response.AttributeDiffs);
        Assert.Equal(1, result.Response.MatchedByPrimaryKey);
        Assert.Equal(1, result.Response.MatchedBySecondaryKey);
    }

    [Fact]
    public void SharedKey_WithASecondaryKeyNamingOtherUnits_IsAmbiguous_EvenIfTheTowerWouldDecide()
    {
        var a = PriceSheet.CondoUnit(1, "101", "9/1", 1, "A");
        var b = PriceSheet.CondoUnit(2, "101", "9/2", 1, "B");
        var c = PriceSheet.CondoUnit(3, "102", "9/3", 1, "C");

        var result = Run([PriceSheet.CondoRow("101", "9/3", tower: "A", appraisal: 5)], a, b, c);

        var error = Assert.Single(result.Response.Errors);
        Assert.Equal(UnitPriceImportErrorCodes.AmbiguousMatch, error.Code);
        Assert.Empty(result.Matched);
    }
}
