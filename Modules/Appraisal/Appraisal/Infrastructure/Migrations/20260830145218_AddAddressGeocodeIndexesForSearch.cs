using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressGeocodeIndexesForSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LandAppraisalDetails_District",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_LandAppraisalDetails_DopaDistrict",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                column: "DopaDistrict",
                filter: "[DopaDistrict] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LandAppraisalDetails_DopaProvince",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                column: "DopaProvince",
                filter: "[DopaProvince] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LandAppraisalDetails_DopaSubDistrict",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                column: "DopaSubDistrict",
                filter: "[DopaSubDistrict] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LandAppraisalDetails_Province",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                column: "Province");

            migrationBuilder.CreateIndex(
                name: "IX_LandAppraisalDetails_SubDistrict",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                column: "SubDistrict");

            migrationBuilder.CreateIndex(
                name: "IX_CondoAppraisalDetails_District",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_CondoAppraisalDetails_DopaDistrict",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                column: "DopaDistrict",
                filter: "[DopaDistrict] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CondoAppraisalDetails_DopaProvince",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                column: "DopaProvince",
                filter: "[DopaProvince] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CondoAppraisalDetails_DopaSubDistrict",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                column: "DopaSubDistrict",
                filter: "[DopaSubDistrict] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CondoAppraisalDetails_Province",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                column: "Province");

            migrationBuilder.CreateIndex(
                name: "IX_CondoAppraisalDetails_SubDistrict",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                column: "SubDistrict");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LandAppraisalDetails_District",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropIndex(
                name: "IX_LandAppraisalDetails_DopaDistrict",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropIndex(
                name: "IX_LandAppraisalDetails_DopaProvince",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropIndex(
                name: "IX_LandAppraisalDetails_DopaSubDistrict",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropIndex(
                name: "IX_LandAppraisalDetails_Province",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropIndex(
                name: "IX_LandAppraisalDetails_SubDistrict",
                schema: "appraisal",
                table: "LandAppraisalDetails");

            migrationBuilder.DropIndex(
                name: "IX_CondoAppraisalDetails_District",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropIndex(
                name: "IX_CondoAppraisalDetails_DopaDistrict",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropIndex(
                name: "IX_CondoAppraisalDetails_DopaProvince",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropIndex(
                name: "IX_CondoAppraisalDetails_DopaSubDistrict",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropIndex(
                name: "IX_CondoAppraisalDetails_Province",
                schema: "appraisal",
                table: "CondoAppraisalDetails");

            migrationBuilder.DropIndex(
                name: "IX_CondoAppraisalDetails_SubDistrict",
                schema: "appraisal",
                table: "CondoAppraisalDetails");
        }
    }
}
