using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameCollateralToPropertyTypeAndAddPurposeCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentRequirements_DocumentTypeId_CollateralTypeCode",
                schema: "appraisal",
                table: "DocumentRequirements");

            migrationBuilder.RenameColumn(
                name: "CollateralTypeCode",
                schema: "appraisal",
                table: "DocumentRequirements",
                newName: "PropertyTypeCode");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentRequirements_CollateralTypeCode",
                schema: "appraisal",
                table: "DocumentRequirements",
                newName: "IX_DocumentRequirements_PropertyTypeCode");

            migrationBuilder.AddColumn<string>(
                name: "PurposeCode",
                schema: "appraisal",
                table: "DocumentRequirements",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequirements_DocumentTypeId_PropertyTypeCode_PurposeCode",
                schema: "appraisal",
                table: "DocumentRequirements",
                columns: new[] { "DocumentTypeId", "PropertyTypeCode", "PurposeCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequirements_PurposeCode",
                schema: "appraisal",
                table: "DocumentRequirements",
                column: "PurposeCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentRequirements_DocumentTypeId_PropertyTypeCode_PurposeCode",
                schema: "appraisal",
                table: "DocumentRequirements");

            migrationBuilder.DropIndex(
                name: "IX_DocumentRequirements_PurposeCode",
                schema: "appraisal",
                table: "DocumentRequirements");

            migrationBuilder.DropColumn(
                name: "PurposeCode",
                schema: "appraisal",
                table: "DocumentRequirements");

            migrationBuilder.RenameColumn(
                name: "PropertyTypeCode",
                schema: "appraisal",
                table: "DocumentRequirements",
                newName: "CollateralTypeCode");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentRequirements_PropertyTypeCode",
                schema: "appraisal",
                table: "DocumentRequirements",
                newName: "IX_DocumentRequirements_CollateralTypeCode");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRequirements_DocumentTypeId_CollateralTypeCode",
                schema: "appraisal",
                table: "DocumentRequirements",
                columns: new[] { "DocumentTypeId", "CollateralTypeCode" },
                unique: true);
        }
    }
}
