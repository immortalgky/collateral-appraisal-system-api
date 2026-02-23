using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCondoAreaDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
