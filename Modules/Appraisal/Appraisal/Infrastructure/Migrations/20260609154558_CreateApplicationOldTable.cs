using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateApplicationOldTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.tables t
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'appraisal' AND t.name = 'ApplicationOld'
                )
                BEGIN
                    CREATE TABLE [appraisal].[ApplicationOld] (
                        [Appplication_id]          [int]            NULL,
                        [Appplication_No]          [varchar](10)    NULL,
                        [Refer_Appno]              [varchar](10)    NULL,
                        [DateRegis]                [datetime]       NULL,
                        [CustomerName]             [nvarchar](200)  NULL,
                        [TypeAsset]                [nvarchar](100)  NULL,
                        [ActNo]                    [nvarchar](200)  NULL,
                        [BuildingNo]               [nvarchar](200)  NULL,
                        [Address]                  [nvarchar](500)  NULL,
                        [Tel]                      [nvarchar](100)  NULL,
                        [Borrower]                 [nvarchar](100)  NULL,
                        [BorrowerDepartment]       [nvarchar](100)  NULL,
                        [TypeApp]                  [nvarchar](100)  NULL,
                        [AmountLimit]              [numeric](25, 2) NULL,
                        [AppName]                  [nvarchar](100)  NULL,
                        [Department]               [nvarchar](100)  NULL,
                        [Assessor]                 [nvarchar](100)  NULL,
                        [Vender]                   [nvarchar](100)  NULL,
                        [Estimates]                [numeric](25, 2) NULL,
                        [DateSurveyCollateral]     [datetime]       NULL,
                        [NumOnBoard]               [int]            NULL,
                        [DateOnBoard]              [datetime]       NULL,
                        [CostEstimateNow]          [numeric](25, 2) NULL,
                        [CostEstimatePercent]      [numeric](25, 2) NULL,
                        [Forcedsale]               [numeric](25, 2) NULL,
                        [InsuranceRates]           [numeric](25, 2) NULL,
                        [MoneyBanks]               [numeric](25, 2) NULL,
                        [CancelFee]                [numeric](25, 2) NULL,
                        [CheckWork]                [int]            NULL,
                        [WorkPercent]              [varchar](20)    NULL,
                        [DateCheckDoc]             [datetime]       NULL,
                        [TotalDateWVender]         [int]            NULL,
                        [DateSumAmount]            [datetime]       NULL,
                        [SumCheckDoc]              [int]            NULL,
                        [DateRec]                  [datetime]       NULL,
                        [SumDocComp]               [int]            NULL,
                        [Comment]                  [nvarchar](1000) NULL,
                        [DateRecDoc]               [datetime]       NULL,
                        [SumDateRec]               [int]            NULL,
                        [Lat]                      [varchar](20)    NULL,
                        [Long]                     [varchar](20)    NULL,
                        [Status]                   [int]            NULL,
                        [PathFile]                 [varchar](200)   NULL
                    ) ON [PRIMARY]
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.tables t
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'appraisal' AND t.name = 'ApplicationOld'
                )
                BEGIN
                    DROP TABLE [appraisal].[ApplicationOld]
                END
                """);
        }
    }
}
