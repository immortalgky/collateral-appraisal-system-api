using Dapper;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// Guards the collateral schema against silent truncation.
///
/// The upsert copies free text — owner names, machine identifiers, project names — straight out of the
/// Appraisal and Auth schemas into the collateral tables. When a destination column is narrower than its
/// source, SQL Server raises error 8152 (String or binary data would be truncated) inside
/// <c>SaveChangesAsync</c>. That path only catches 2627/2601, so an 8152 escapes as an unhandled
/// exception and dead-letters AppraisalCompletedConsumer — no user-visible signal, the collateral simply
/// never appears. Thai text makes it likelier: the same field holds more characters than an English test
/// fixture would.
///
/// Migration 20260809140957_AlignCollateralColumnWidthsWithAppraisal widened every column that was short,
/// so today every pair below holds. These tests exist so that stays true: a future migration that narrows
/// a collateral column, or one that widens an appraisal column without widening its collateral
/// counterpart, fails here rather than in production.
///
/// Reading sys.columns (not the EF model) is deliberate — it is the applied schema that decides whether
/// the INSERT succeeds, and hand-authored DbUp scripts can move a width without touching the model.
///
/// To add a pair: append an InlineData row. No change to the test body is needed.
/// </summary>
[Collection("Integration")]
public class CollateralColumnWidthAlignmentTests(IntegrationTestFixture fixture)
{
    /// <summary>Character length of a column, or -1 for MAX. Throws when the column does not exist.</summary>
    private static async Task<int> GetWidthAsync(
        ISqlConnectionFactory factory, string schema, string table, string column)
    {
        var connection = factory.GetOpenConnection();

        var maxLength = await connection.QueryFirstOrDefaultAsync<short?>(
            """
            SELECT c.max_length
            FROM sys.columns c
            JOIN sys.tables  t ON t.object_id = c.object_id
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = @Schema AND t.name = @Table AND c.name = @Column
            """,
            new { Schema = schema, Table = table, Column = column });

        Assert.True(
            maxLength.HasValue,
            $"{schema}.{table}.{column} does not exist. If it was renamed, update this test's pair list — " +
            "leaving it out removes the truncation guard for that field.");

        // sys.columns reports bytes; nvarchar stores 2 per character. MAX is reported as -1.
        return maxLength!.Value == -1 ? -1 : maxLength.Value / 2;
    }

    /// <param name="dstSchema">Collateral-side column the upsert writes into.</param>
    /// <param name="srcSchema">Upstream column the value is read from.</param>
    [Theory]
    // Owner name is copied verbatim (no concatenation) from whichever appraisal detail matches the type.
    [InlineData("collateral", "CollateralMasters", "OwnerName", "appraisal", "LandAppraisalDetails", "OwnerName")]
    [InlineData("collateral", "CollateralMasters", "OwnerName", "appraisal", "CondoAppraisalDetails", "OwnerName")]
    [InlineData("collateral", "CollateralMasters", "OwnerName", "appraisal", "BuildingAppraisalDetails", "OwnerName")]
    [InlineData("collateral", "CollateralMasters", "OwnerName", "appraisal", "MachineryAppraisalDetails", "OwnerName")]
    // Machine identifiers — these also form the machine dedup key, so a truncation would additionally
    // merge two different machines into one master.
    [InlineData("collateral", "MachineDetails", "MachineRegistrationNo", "appraisal", "MachineryAppraisalDetails", "RegistrationNumber")]
    [InlineData("collateral", "MachineDetails", "SerialNo", "appraisal", "MachineryAppraisalDetails", "SerialNo")]
    [InlineData("collateral", "MachineDetails", "Brand", "appraisal", "MachineryAppraisalDetails", "Brand")]
    [InlineData("collateral", "MachineDetails", "Model", "appraisal", "MachineryAppraisalDetails", "Model")]
    [InlineData("collateral", "MachineDetails", "Manufacturer", "appraisal", "MachineryAppraisalDetails", "Manufacturer")]
    // Leasehold — Lessor/Lessee are part of that dedup key for the same reason.
    [InlineData("collateral", "LeaseholdDetails", "Lessor", "appraisal", "LeaseAgreementDetails", "LessorName")]
    [InlineData("collateral", "LeaseholdDetails", "Lessee", "appraisal", "LeaseAgreementDetails", "LesseeName")]
    [InlineData("collateral", "LeaseholdDetails", "LeaseRegistrationNo", "appraisal", "LeaseAgreementDetails", "ContractNo")]
    // Descriptive names.
    [InlineData("collateral", "CondoDetails", "CondoName", "appraisal", "CondoAppraisalDetails", "CondoName")]
    [InlineData("collateral", "ProjectDetails", "ProjectName", "appraisal", "Projects", "ProjectName")]
    // The engagement freezes the external company name resolved by id from auth.Companies.
    [InlineData("collateral", "CollateralEngagements", "AppraisalCompanyName", "auth", "Companies", "Name")]
    // Last-resort fallback when the appraiser's username does not resolve to an auth user.
    [InlineData("collateral", "CollateralEngagements", "InternalAppraiserName", "appraisal", "AppraisalAssignments", "InternalAppraiserName")]
    public async Task DestinationColumn_IsAtLeastAsWideAsItsSource(
        string dstSchema, string dstTable, string dstColumn,
        string srcSchema, string srcTable, string srcColumn)
    {
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();

        var destination = await GetWidthAsync(factory, dstSchema, dstTable, dstColumn);
        var source      = await GetWidthAsync(factory, srcSchema, srcTable, srcColumn);

        // A MAX destination always fits; a MAX source never does.
        if (destination == -1) return;

        Assert.True(
            source != -1 && destination >= source,
            $"{dstSchema}.{dstTable}.{dstColumn} is nvarchar({destination}) but reads from " +
            $"{srcSchema}.{srcTable}.{srcColumn} which is nvarchar({(source == -1 ? "max" : source.ToString())}). " +
            $"Values longer than {destination} characters raise SQL error 8152 during the collateral " +
            "upsert and dead-letters the consumer. Widen the collateral column to match.");
    }

    /// <summary>
    /// The one value in this path that is BUILT rather than copied: GetAppraisalForCollateralQueryHandler
    /// resolves the bank appraiser as CONCAT(FirstName, ' ', LastName), so the destination has to hold
    /// both names plus the separator — checking it against either name alone would pass while still
    /// overflowing at roughly double the length.
    /// </summary>
    [Fact]
    public async Task InternalAppraiserName_FitsFirstNameSpaceLastName()
    {
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();

        var destination = await GetWidthAsync(factory, "collateral", "CollateralEngagements", "InternalAppraiserName");
        var firstName   = await GetWidthAsync(factory, "auth", "AspNetUsers", "FirstName");
        var lastName    = await GetWidthAsync(factory, "auth", "AspNetUsers", "LastName");

        var required = firstName + 1 + lastName;

        Assert.True(
            destination >= required,
            $"collateral.CollateralEngagements.InternalAppraiserName is nvarchar({destination}) but holds " +
            $"CONCAT(FirstName, ' ', LastName) from auth.AspNetUsers, which needs {required} " +
            $"({firstName} + 1 + {lastName}). Widen it, or trim the value before it reaches the engagement.");
    }
}
