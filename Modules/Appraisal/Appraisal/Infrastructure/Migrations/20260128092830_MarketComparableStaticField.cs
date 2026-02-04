using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MarketComparableStaticField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketComparables_Province",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropIndex(
                name: "IX_MarketComparables_Status",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "Address",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "DataConfidence",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "DataSource",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "District",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "Latitude",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "Longitude",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "PricePerUnit",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "SubDistrict",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "SurveyDate",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "SurveyedBy",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "TransactionDate",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "TransactionPrice",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "UnitType",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.DropColumn(
                name: "VerifiedBy",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.RenameColumn(
                name: "VerifiedAt",
                schema: "appraisal",
                table: "MarketComparables",
                newName: "InfoDateTime");

            migrationBuilder.RenameColumn(
                name: "Province",
                schema: "appraisal",
                table: "MarketComparables",
                newName: "SurveyName");

            migrationBuilder.AddColumn<string>(
                name: "SourceInfo",
                schema: "appraisal",
                table: "MarketComparables",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceInfo",
                schema: "appraisal",
                table: "MarketComparables");

            migrationBuilder.RenameColumn(
                name: "SurveyName",
                schema: "appraisal",
                table: "MarketComparables",
                newName: "Province");

            migrationBuilder.RenameColumn(
                name: "InfoDateTime",
                schema: "appraisal",
                table: "MarketComparables",
                newName: "VerifiedAt");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "appraisal",
                table: "MarketComparables",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataConfidence",
                schema: "appraisal",
                table: "MarketComparables",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                schema: "appraisal",
                table: "MarketComparables",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "appraisal",
                table: "MarketComparables",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                schema: "appraisal",
                table: "MarketComparables",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                schema: "appraisal",
                table: "MarketComparables",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                schema: "appraisal",
                table: "MarketComparables",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                schema: "appraisal",
                table: "MarketComparables",
                type: "decimal(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                schema: "appraisal",
                table: "MarketComparables",
                type: "decimal(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerUnit",
                schema: "appraisal",
                table: "MarketComparables",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "appraisal",
                table: "MarketComparables",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubDistrict",
                schema: "appraisal",
                table: "MarketComparables",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SurveyDate",
                schema: "appraisal",
                table: "MarketComparables",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "SurveyedBy",
                schema: "appraisal",
                table: "MarketComparables",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TransactionDate",
                schema: "appraisal",
                table: "MarketComparables",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionPrice",
                schema: "appraisal",
                table: "MarketComparables",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionType",
                schema: "appraisal",
                table: "MarketComparables",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitType",
                schema: "appraisal",
                table: "MarketComparables",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VerifiedBy",
                schema: "appraisal",
                table: "MarketComparables",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparables_Province",
                schema: "appraisal",
                table: "MarketComparables",
                column: "Province");

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparables_Status",
                schema: "appraisal",
                table: "MarketComparables",
                column: "Status");
        }
    }
}
