using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeStructurePriceTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeeStructures_FeeCode",
                schema: "appraisal",
                table: "FeeStructures");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxSellingPrice",
                schema: "appraisal",
                table: "FeeStructures",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinSellingPrice",
                schema: "appraisal",
                table: "FeeStructures",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "BaseAmount", "MaxSellingPrice", "MinSellingPrice" },
                values: new object[] { 3500m, 5000000m, 0m });

            migrationBuilder.UpdateData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                columns: new[] { "MaxSellingPrice", "MinSellingPrice" },
                values: new object[] { null, 0m });

            migrationBuilder.UpdateData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                columns: new[] { "MaxSellingPrice", "MinSellingPrice" },
                values: new object[] { null, 0m });

            migrationBuilder.InsertData(
                schema: "appraisal",
                table: "FeeStructures",
                columns: new[] { "Id", "BaseAmount", "CreatedAt", "CreatedBy", "CreatedWorkstation", "FeeCode", "FeeName", "IsActive", "MaxSellingPrice", "MinSellingPrice", "UpdatedAt", "UpdatedBy", "UpdatedWorkstation" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000004"), 5000m, null, "System", null, "01", "Appraisal Fee", true, 10000000m, 5000001m, null, null, null },
                    { new Guid("00000000-0000-0000-0000-000000000005"), 7000m, null, "System", null, "01", "Appraisal Fee", true, null, 10000001m, null, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructures_FeeCode_MinSellingPrice",
                schema: "appraisal",
                table: "FeeStructures",
                columns: new[] { "FeeCode", "MinSellingPrice" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeeStructures_FeeCode_MinSellingPrice",
                schema: "appraisal",
                table: "FeeStructures");

            migrationBuilder.DeleteData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"));

            migrationBuilder.DropColumn(
                name: "MaxSellingPrice",
                schema: "appraisal",
                table: "FeeStructures");

            migrationBuilder.DropColumn(
                name: "MinSellingPrice",
                schema: "appraisal",
                table: "FeeStructures");

            migrationBuilder.UpdateData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "BaseAmount",
                value: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructures_FeeCode",
                schema: "appraisal",
                table: "FeeStructures",
                column: "FeeCode",
                unique: true);
        }
    }
}
