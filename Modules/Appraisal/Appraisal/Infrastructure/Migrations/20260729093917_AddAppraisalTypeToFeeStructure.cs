using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalTypeToFeeStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeeStructures_FeeCode_MinSellingPrice",
                schema: "appraisal",
                table: "FeeStructures");

            migrationBuilder.AddColumn<string>(
                name: "AppraisalType",
                schema: "appraisal",
                table: "FeeStructures",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "AppraisalType", "BaseAmount", "MaxSellingPrice" },
                values: new object[] { null, 2500m, 7000000m });

            migrationBuilder.UpdateData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                columns: new[] { "AppraisalType", "BaseAmount", "MinSellingPrice" },
                values: new object[] { null, 3000m, 7000001m });

            migrationBuilder.UpdateData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                columns: new[] { "AppraisalType", "BaseAmount" },
                values: new object[] { null, 3500m });

            migrationBuilder.InsertData(
                schema: "appraisal",
                table: "FeeStructures",
                columns: new[] { "Id", "AppraisalType", "BaseAmount", "CreatedAt", "CreatedBy", "CreatedWorkstation", "FeeCode", "IsActive", "MaxSellingPrice", "MinSellingPrice", "UpdatedAt", "UpdatedBy", "UpdatedWorkstation" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), "PreAppraisal", 10000m, null, "System", null, "01", true, null, 0m, null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructures_FeeCode_AppraisalType_MinSellingPrice",
                schema: "appraisal",
                table: "FeeStructures",
                columns: new[] { "FeeCode", "AppraisalType", "MinSellingPrice" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeeStructures_FeeCode_AppraisalType_MinSellingPrice",
                schema: "appraisal",
                table: "FeeStructures");

            migrationBuilder.DeleteData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"));

            migrationBuilder.DropColumn(
                name: "AppraisalType",
                schema: "appraisal",
                table: "FeeStructures");

            migrationBuilder.UpdateData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "BaseAmount", "MaxSellingPrice" },
                values: new object[] { 3500m, 5000000m });

            migrationBuilder.UpdateData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                columns: new[] { "BaseAmount", "MinSellingPrice" },
                values: new object[] { 5000m, 5000001m });

            migrationBuilder.UpdateData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "BaseAmount",
                value: 7000m);

            migrationBuilder.CreateIndex(
                name: "IX_FeeStructures_FeeCode_MinSellingPrice",
                schema: "appraisal",
                table: "FeeStructures",
                columns: new[] { "FeeCode", "MinSellingPrice" },
                unique: true);
        }
    }
}
