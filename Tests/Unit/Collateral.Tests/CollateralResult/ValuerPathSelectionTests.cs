using Collateral.CollateralMasters.CollateralResult;
using Collateral.Contracts.FileInterface;
using Integration.FileInterface.Format.CollateralResult;

namespace Collateral.Tests.CollateralResult;

/// <summary>
/// Covers <see cref="CollateralResultQuery.SelectValuerFields"/> and the blank columns it produces.
///
/// An appraisal ran on the External path or the Internal path, never both, so a Detail record carries
/// one valuer pair and blanks the other. The engagement cannot be trusted to have nulled the unused
/// pair itself: on the External path <c>AppraiserUserId</c> / <c>InternalAppraiserName</c> still hold
/// the bank's follow-up officer (or the company's own appraiser), and an off-system engagement holds
/// both pairs outright.
/// </summary>
public class ValuerPathSelectionTests
{
    private static readonly Guid Company = Guid.Parse("6ce2b1f2-6b0d-4d8f-9a0f-2c9a1a8f0001");

    [Fact]
    public void ExternalPathKeepsTheCompanyAndBlanksTheInternalPair()
    {
        var fields = CollateralResultQuery.SelectValuerFields(
            appraisalCompanyId: Company,
            internalValuerCode: "6327",
            internalAppraiserName: "Somchai Jaidee",
            appraisalCompanyCode: "KTAC",
            appraisalCompanyName: "K-TAC Appraisal and Services");

        Assert.Null(fields.InternalCode);
        Assert.Null(fields.InternalName);
        Assert.Equal("KTAC", fields.ExternalCode);
        Assert.Equal("K-TAC Appraisal and Services", fields.ExternalName);
    }

    [Fact]
    public void OffSystemEngagementIsExternalEvenThoughABankStafferKeyedTheBookIn()
    {
        // EXTO: AssignmentType stays External and AssigneeCompanyId is kept, but AssigneeUserId holds
        // the internal keyer — which is exactly how both pairs used to go out filled.
        var fields = CollateralResultQuery.SelectValuerFields(
            appraisalCompanyId: Company,
            internalValuerCode: "6327",
            internalAppraiserName: "Bank Keyer",
            appraisalCompanyCode: "KTAC",
            appraisalCompanyName: "K-TAC Appraisal and Services");

        Assert.Null(fields.InternalCode);
        Assert.Null(fields.InternalName);
        Assert.Equal("KTAC", fields.ExternalCode);
    }

    [Fact]
    public void InternalPathKeepsTheStaffValuerAndBlanksTheExternalPair()
    {
        var fields = CollateralResultQuery.SelectValuerFields(
            appraisalCompanyId: null,
            internalValuerCode: "6327",
            internalAppraiserName: "Somchai Jaidee",
            // Stale company values on an internal engagement must not leak out.
            appraisalCompanyCode: "KTAC",
            appraisalCompanyName: "K-TAC Appraisal and Services");

        Assert.Equal("6327", fields.InternalCode);
        Assert.Equal("Somchai Jaidee", fields.InternalName);
        Assert.Null(fields.ExternalCode);
        Assert.Null(fields.ExternalName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExactlyOnePairIsEverPopulated(bool external)
    {
        var fields = CollateralResultQuery.SelectValuerFields(
            appraisalCompanyId: external ? Company : null,
            internalValuerCode: "6327",
            internalAppraiserName: "Somchai Jaidee",
            appraisalCompanyCode: "KTAC",
            appraisalCompanyName: "K-TAC Appraisal and Services");

        var hasInternal = fields.InternalCode is not null || fields.InternalName is not null;
        var hasExternal = fields.ExternalCode is not null || fields.ExternalName is not null;

        Assert.NotEqual(hasInternal, hasExternal);
    }

    // --- The columns the host actually reads (1-based positions 107-150 / 151-194) ---

    private static string BuildDetail(
        string? internalCode, string? internalName, string? externalCode, string? externalName) =>
        new CollateralResultFileWriter().BuildDetail(new CollateralResultRow(
            AppraisalId: Guid.NewGuid(),
            CollateralId: "25909",
            AppraisalReportNumber: "6800123",
            AppraisalValue: 4500000.00m,
            LandValue: 1500000.00m,
            BuildingValue: 3000000.00m,
            ForceSaleValue: 4400000.00m,
            CurrentAppraisalDate: new DateOnly(2025, 1, 21),
            NextAppraisalDate: new DateOnly(2028, 1, 21),
            InternalValuerCode: internalCode,
            InternalValuerName: internalName,
            ExternalValuerCode: externalCode,
            ExternalValuerName: externalName,
            LifeYear: null,
            AppraisalStatus: "A",
            BuildingAge: 12,
            AreaUtilization: 250.50m));

    [Fact]
    public void ExternalRecordLeavesPositions107To150Blank()
    {
        var fields = CollateralResultQuery.SelectValuerFields(
            Company, "6327", "Somchai Jaidee", "KTAC", "บริษัท เคแทค แอพเพรซัล แอนด์ เซอร์วิส");

        var line = BuildDetail(
            fields.InternalCode, fields.InternalName, fields.ExternalCode, fields.ExternalName);

        Assert.Equal(new string(' ', 44), line[106..150]);                       // internal pair blank
        Assert.Equal("KTAC", line[150..154]);                                    // external code
        Assert.Equal("บริษัท เคแทค แอพเพรซัล แอนด์ เซอร์วิส".PadRight(40), line[154..194]);
    }

    [Fact]
    public void InternalRecordLeavesPositions151To194Blank()
    {
        var fields = CollateralResultQuery.SelectValuerFields(
            null, "6327", "Somchai Jaidee", "KTAC", "K-TAC Appraisal and Services");

        var line = BuildDetail(
            fields.InternalCode, fields.InternalName, fields.ExternalCode, fields.ExternalName);

        Assert.Equal("6327", line[106..110]);
        Assert.Equal("Somchai Jaidee".PadRight(40), line[110..150]);
        Assert.Equal(new string(' ', 44), line[150..194]);                       // external pair blank
    }
}
