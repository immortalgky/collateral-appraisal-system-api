using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeMarketComparableImageToDocumentReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileName",
                schema: "appraisal",
                table: "MarketComparableImages");

            migrationBuilder.DropColumn(
                name: "FilePath",
                schema: "appraisal",
                table: "MarketComparableImages");

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId",
                schema: "appraisal",
                table: "MarketComparableImages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_MarketComparableImages_DocumentId",
                schema: "appraisal",
                table: "MarketComparableImages",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketComparableImages_DocumentId",
                schema: "appraisal",
                table: "MarketComparableImages");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                schema: "appraisal",
                table: "MarketComparableImages");

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                schema: "appraisal",
                table: "MarketComparableImages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                schema: "appraisal",
                table: "MarketComparableImages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
