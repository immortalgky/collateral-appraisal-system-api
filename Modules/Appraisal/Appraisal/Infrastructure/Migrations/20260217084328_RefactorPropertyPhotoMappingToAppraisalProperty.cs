using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPropertyPhotoMappingToAppraisalProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PropertyPhotoMappings_PropertyDetailType_PropertyDetailId",
                schema: "appraisal",
                table: "PropertyPhotoMappings");

            migrationBuilder.DropColumn(
                name: "PropertyDetailType",
                schema: "appraisal",
                table: "PropertyPhotoMappings");

            migrationBuilder.RenameColumn(
                name: "PropertyDetailId",
                schema: "appraisal",
                table: "PropertyPhotoMappings",
                newName: "AppraisalPropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyPhotoMappings_AppraisalPropertyId",
                schema: "appraisal",
                table: "PropertyPhotoMappings",
                column: "AppraisalPropertyId");

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyPhotoMappings_AppraisalProperties_AppraisalPropertyId",
                schema: "appraisal",
                table: "PropertyPhotoMappings",
                column: "AppraisalPropertyId",
                principalSchema: "appraisal",
                principalTable: "AppraisalProperties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PropertyPhotoMappings_AppraisalProperties_AppraisalPropertyId",
                schema: "appraisal",
                table: "PropertyPhotoMappings");

            migrationBuilder.DropIndex(
                name: "IX_PropertyPhotoMappings_AppraisalPropertyId",
                schema: "appraisal",
                table: "PropertyPhotoMappings");

            migrationBuilder.RenameColumn(
                name: "AppraisalPropertyId",
                schema: "appraisal",
                table: "PropertyPhotoMappings",
                newName: "PropertyDetailId");

            migrationBuilder.AddColumn<string>(
                name: "PropertyDetailType",
                schema: "appraisal",
                table: "PropertyPhotoMappings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyPhotoMappings_PropertyDetailType_PropertyDetailId",
                schema: "appraisal",
                table: "PropertyPhotoMappings",
                columns: new[] { "PropertyDetailType", "PropertyDetailId" });
        }
    }
}
