using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateAS400ReportListingTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.tables t
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'Appraisal' AND t.name = 'AS400ReportListing'
                )
                BEGIN
                    CREATE TABLE [Appraisal].[AS400ReportListing] (
                        [RecordType]                      [nchar](1)       NOT NULL,
                        [ApplicationId]                   [nchar](10)      NULL,
                        [NewestApplicationId]             [nchar](10)      NULL,
                        [CollateralID]                    [decimal](19, 0) NULL,
                        [UnderConstruction]               [nchar](1)       NULL,
                        [ProcessOfConstruction]           [decimal](5, 2)  NULL,
                        [AppraisalValueAsCompleted]       [decimal](15, 2) NULL,
                        [AppraisalValueAtTheOrigination]  [decimal](15, 2) NULL,
                        [ValuationDate]                   [date]           NULL,
                        [ValuationPriceInBaht]            [decimal](15, 2) NULL
                    ) ON [PRIMARY]
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.tables t
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'Appraisal' AND t.name = 'AS400ReportListing'
                )
                BEGIN
                    DROP TABLE [Appraisal].[AS400ReportListing]
                END
                """);
        }
    }
}
