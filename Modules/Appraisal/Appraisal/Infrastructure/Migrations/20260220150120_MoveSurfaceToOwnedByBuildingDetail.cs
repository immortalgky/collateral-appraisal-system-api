using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveSurfaceToOwnedByBuildingDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PricePerSqM",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "PricePerSqMBeforeDepreciation");

            migrationBuilder.RenameColumn(
                name: "AppraisalMethod",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "DepreciationMethod");

            migrationBuilder.RenameColumn(
                name: "ToFloorNo",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "ToFloorNumber");

            migrationBuilder.RenameColumn(
                name: "FromFloorNo",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "FromFloorNumber");

            migrationBuilder.RenameColumn(
                name: "FloorSurfaceOther",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "FloorSurfaceTypeOther");

            migrationBuilder.RenameColumn(
                name: "FloorSurface",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "FloorSurfaceType");

            migrationBuilder.RenameColumn(
                name: "FloorStructureOther",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "FloorStructureTypeOther");

            migrationBuilder.RenameColumn(
                name: "FloorStructure",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "FloorStructureType");

            migrationBuilder.RenameColumn(
                name: "AppraisalPropertyId",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "BuildingAppraisalDetailId");

            migrationBuilder.RenameIndex(
                name: "IX_BuildingAppraisalSurfaces_AppraisalPropertyId",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "IX_BuildingAppraisalSurfaces_BuildingAppraisalDetailId");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingAppraisalSurfaces_BuildingAppraisalDetails_BuildingAppraisalDetailId",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                column: "BuildingAppraisalDetailId",
                principalSchema: "appraisal",
                principalTable: "BuildingAppraisalDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BuildingAppraisalSurfaces_BuildingAppraisalDetails_BuildingAppraisalDetailId",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces");

            migrationBuilder.RenameColumn(
                name: "PricePerSqMBeforeDepreciation",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "PricePerSqM");

            migrationBuilder.RenameColumn(
                name: "DepreciationMethod",
                schema: "appraisal",
                table: "BuildingDepreciationDetails",
                newName: "AppraisalMethod");

            migrationBuilder.RenameColumn(
                name: "ToFloorNumber",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "ToFloorNo");

            migrationBuilder.RenameColumn(
                name: "FromFloorNumber",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "FromFloorNo");

            migrationBuilder.RenameColumn(
                name: "FloorSurfaceTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "FloorSurfaceOther");

            migrationBuilder.RenameColumn(
                name: "FloorSurfaceType",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "FloorSurface");

            migrationBuilder.RenameColumn(
                name: "FloorStructureTypeOther",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "FloorStructureOther");

            migrationBuilder.RenameColumn(
                name: "FloorStructureType",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "FloorStructure");

            migrationBuilder.RenameColumn(
                name: "BuildingAppraisalDetailId",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "AppraisalPropertyId");

            migrationBuilder.RenameIndex(
                name: "IX_BuildingAppraisalSurfaces_BuildingAppraisalDetailId",
                schema: "appraisal",
                table: "BuildingAppraisalSurfaces",
                newName: "IX_BuildingAppraisalSurfaces_AppraisalPropertyId");
        }
    }
}
