using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdjustCondoFieldName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpperFloorMaterialOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "UpperFloorMaterialTypeOther");

            migrationBuilder.RenameColumn(
                name: "UpperFloorMaterial",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "UpperFloorMaterialType");

            migrationBuilder.RenameColumn(
                name: "RoomNo",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "RoomNumber");

            migrationBuilder.RenameColumn(
                name: "RoomLayoutOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "RoomLayoutTypeOther");

            migrationBuilder.RenameColumn(
                name: "RoomLayout",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "RoomLayoutType");

            migrationBuilder.RenameColumn(
                name: "RoofOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "RoofTypeOther");

            migrationBuilder.RenameColumn(
                name: "Roof",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "RoofType");

            migrationBuilder.RenameColumn(
                name: "PublicUtilityOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "PublicUtilityTypeOther");

            migrationBuilder.RenameColumn(
                name: "PublicUtility",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "PublicUtilityType");

            migrationBuilder.RenameColumn(
                name: "LocationView",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "LocationViewType");

            migrationBuilder.RenameColumn(
                name: "GroundFloorMaterialOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "GroundFloorMaterialTypeOther");

            migrationBuilder.RenameColumn(
                name: "GroundFloorMaterial",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "GroundFloorMaterialType");

            migrationBuilder.RenameColumn(
                name: "FloorNo",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "FloorNumber");

            migrationBuilder.RenameColumn(
                name: "Environment",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "LocationType");

            migrationBuilder.RenameColumn(
                name: "DocValidate",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "IsDocumentValidated");

            migrationBuilder.RenameColumn(
                name: "DecorationOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "FacilityTypeOther");

            migrationBuilder.RenameColumn(
                name: "Decoration",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "DecorationType");

            migrationBuilder.RenameColumn(
                name: "ConstMaterial",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "ConstructionMaterialType");

            migrationBuilder.RenameColumn(
                name: "CondoRegisNo",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "CondoRegistrationNumber");

            migrationBuilder.RenameColumn(
                name: "CondoLocation",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "FacilityType");

            migrationBuilder.RenameColumn(
                name: "CondoFacilityOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "DecorationTypeOther");

            migrationBuilder.RenameColumn(
                name: "CondoFacility",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "EnvironmentType");

            migrationBuilder.RenameColumn(
                name: "BuiltOnTitleNo",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "BuiltOnTitleNumber");

            migrationBuilder.RenameColumn(
                name: "BuildingYear",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "ConstructionYear");

            migrationBuilder.RenameColumn(
                name: "BuildingForm",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "BuildingFormType");

            migrationBuilder.RenameColumn(
                name: "BuildingCondition",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "BuildingConditionType");

            migrationBuilder.RenameColumn(
                name: "BathroomFloorMaterialOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "BathroomFloorMaterialTypeOther");

            migrationBuilder.RenameColumn(
                name: "BathroomFloorMaterial",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "BathroomFloorMaterialType");

            migrationBuilder.RenameColumn(
                name: "BuildingCondition",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "BuildingConditionType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpperFloorMaterialTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "UpperFloorMaterialOther");

            migrationBuilder.RenameColumn(
                name: "UpperFloorMaterialType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "UpperFloorMaterial");

            migrationBuilder.RenameColumn(
                name: "RoomNumber",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "RoomNo");

            migrationBuilder.RenameColumn(
                name: "RoomLayoutTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "RoomLayoutOther");

            migrationBuilder.RenameColumn(
                name: "RoomLayoutType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "RoomLayout");

            migrationBuilder.RenameColumn(
                name: "RoofTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "RoofOther");

            migrationBuilder.RenameColumn(
                name: "RoofType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "Roof");

            migrationBuilder.RenameColumn(
                name: "PublicUtilityTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "PublicUtilityOther");

            migrationBuilder.RenameColumn(
                name: "PublicUtilityType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "PublicUtility");

            migrationBuilder.RenameColumn(
                name: "LocationViewType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "LocationView");

            migrationBuilder.RenameColumn(
                name: "LocationType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "Environment");

            migrationBuilder.RenameColumn(
                name: "IsDocumentValidated",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "DocValidate");

            migrationBuilder.RenameColumn(
                name: "GroundFloorMaterialTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "GroundFloorMaterialOther");

            migrationBuilder.RenameColumn(
                name: "GroundFloorMaterialType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "GroundFloorMaterial");

            migrationBuilder.RenameColumn(
                name: "FloorNumber",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "FloorNo");

            migrationBuilder.RenameColumn(
                name: "FacilityTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "DecorationOther");

            migrationBuilder.RenameColumn(
                name: "FacilityType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "CondoLocation");

            migrationBuilder.RenameColumn(
                name: "EnvironmentType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "CondoFacility");

            migrationBuilder.RenameColumn(
                name: "DecorationTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "CondoFacilityOther");

            migrationBuilder.RenameColumn(
                name: "DecorationType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "Decoration");

            migrationBuilder.RenameColumn(
                name: "ConstructionYear",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "BuildingYear");

            migrationBuilder.RenameColumn(
                name: "ConstructionMaterialType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "ConstMaterial");

            migrationBuilder.RenameColumn(
                name: "CondoRegistrationNumber",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "CondoRegisNo");

            migrationBuilder.RenameColumn(
                name: "BuiltOnTitleNumber",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "BuiltOnTitleNo");

            migrationBuilder.RenameColumn(
                name: "BuildingFormType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "BuildingForm");

            migrationBuilder.RenameColumn(
                name: "BuildingConditionType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "BuildingCondition");

            migrationBuilder.RenameColumn(
                name: "BathroomFloorMaterialTypeOther",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "BathroomFloorMaterialOther");

            migrationBuilder.RenameColumn(
                name: "BathroomFloorMaterialType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                newName: "BathroomFloorMaterial");

            migrationBuilder.RenameColumn(
                name: "BuildingConditionType",
                schema: "appraisal",
                table: "BuildingAppraisalDetails",
                newName: "BuildingCondition");
        }
    }
}
