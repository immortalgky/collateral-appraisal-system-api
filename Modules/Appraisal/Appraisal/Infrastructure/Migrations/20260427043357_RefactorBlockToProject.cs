using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorBlockToProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CondoModelAreaDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CondoModelAssumptions",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CondoProjects",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CondoUnitPrices",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CondoUnitUploads",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "VillageModelAreaDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "VillageModelAssumptions",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "VillageModelDepreciationPeriods",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "VillageModelSurfaces",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "VillageProjectLandTitles",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "VillageProjects",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "VillageUnitPrices",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "VillageUnitUploads",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CondoPricingAssumptions",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CondoUnits",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "VillagePricingAssumptions",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "VillageModelDepreciationDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "VillageProjectLands",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "VillageUnits",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CondoModels",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CondoTowers",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "VillageModels",
                schema: "appraisal");

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectType = table.Column<int>(type: "int", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProjectDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Developer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProjectSaleLaunchDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LandAreaRai = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    LandAreaNgan = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    LandAreaWa = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    UnitForSaleCount = table.Column<int>(type: "int", nullable: true),
                    NumberOfPhase = table.Column<int>(type: "int", nullable: true),
                    LandOffice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AddressLandOffice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LocationNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Road = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Utilities = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    UtilitiesOther = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Facilities = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    FacilitiesOther = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BuiltOnTitleDeedNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LicenseExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectLands",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandOffice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsOwnerVerified = table.Column<bool>(type: "bit", nullable: true),
                    HasObligation = table.Column<bool>(type: "bit", nullable: true),
                    ObligationDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsLandLocationVerified = table.Column<bool>(type: "bit", nullable: true),
                    LandCheckMethodType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandCheckMethodTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DistanceFromMainRoad = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Village = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LandShapeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UrbanPlanningType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandZoneType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    LandZoneTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PlotLocationType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    PlotLocationTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LandFillType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandFillTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LandFillPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    SoilLevel = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    AccessRoadWidth = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    RightOfWay = table.Column<short>(type: "smallint", nullable: true),
                    RoadFrontage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    NumberOfSidesFacingRoad = table.Column<int>(type: "int", nullable: true),
                    RoadPassInFrontOfLand = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandAccessibilityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandAccessibilityRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RoadSurfaceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoadSurfaceTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    HasElectricity = table.Column<bool>(type: "bit", nullable: true),
                    ElectricityDistance = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PublicUtilityType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    PublicUtilityTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LandUseType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    LandUseTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LandEntranceExitType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    LandEntranceExitTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TransportationAccessType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    TransportationAccessTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PropertyAnticipationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PropertyAnticipationTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsExpropriated = table.Column<bool>(type: "bit", nullable: true),
                    ExpropriationRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsInExpropriationLine = table.Column<bool>(type: "bit", nullable: true),
                    ExpropriationLineRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RoyalDecree = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsEncroached = table.Column<bool>(type: "bit", nullable: true),
                    EncroachmentRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EncroachmentArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    IsLandlocked = table.Column<bool>(type: "bit", nullable: true),
                    LandlockedRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsForestBoundary = table.Column<bool>(type: "bit", nullable: true),
                    ForestBoundaryRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OtherLegalLimitations = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EvictionType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    EvictionTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AllocationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NorthAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NorthBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    SouthAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SouthBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    EastAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EastBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    WestAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WestBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PondArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PondDepth = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    HasBuilding = table.Column<bool>(type: "bit", nullable: true),
                    HasBuildingOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectLands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectLands_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "appraisal",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectModels",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ModelDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BuildingNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NumberOfHouse = table.Column<int>(type: "int", nullable: true),
                    StartingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StartingPriceMin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StartingPriceMax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StandardPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    HasMezzanine = table.Column<bool>(type: "bit", nullable: true),
                    UsableAreaMin = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    UsableAreaMax = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    StandardUsableArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    FireInsuranceCondition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoomLayoutType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoomLayoutTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GroundFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GroundFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    UpperFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpperFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BathroomFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BathroomFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ImageDocumentIds = table.Column<string>(type: "nvarchar(2000)", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LandAreaRai = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    LandAreaNgan = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    LandAreaWa = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    StandardLandArea = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    BuildingType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildingTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NumberOfFloors = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: true),
                    DecorationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DecorationTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsEncroachingOthers = table.Column<bool>(type: "bit", nullable: true),
                    EncroachingOthersRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EncroachingOthersArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    BuildingMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildingStyleType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsResidential = table.Column<bool>(type: "bit", nullable: true),
                    BuildingAge = table.Column<int>(type: "int", nullable: true),
                    ConstructionYear = table.Column<int>(type: "int", nullable: true),
                    ResidentialRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ConstructionStyleType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionStyleRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StructureType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    StructureTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RoofFrameType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    RoofFrameTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RoofType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    RoofTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CeilingType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    CeilingTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    InteriorWallType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    InteriorWallTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ExteriorWallType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    ExteriorWallTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FenceType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    FenceTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ConstructionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    UtilizationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UtilizationTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectModels_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "appraisal",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPricingAssumptions",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationMethod = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CornerAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EdgeAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OtherAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ForceSalePercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    PoolViewAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SouthAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FloorIncrementEveryXFloor = table.Column<int>(type: "int", nullable: true),
                    FloorIncrementAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NearGardenAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LandIncreaseDecreaseRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectPricingAssumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectPricingAssumptions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "appraisal",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTowers",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TowerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NumberOfUnits = table.Column<int>(type: "int", nullable: true),
                    NumberOfFloors = table.Column<int>(type: "int", nullable: true),
                    CondoRegistrationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModelTypeIds = table.Column<string>(type: "nvarchar(2000)", nullable: true),
                    ConditionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HasObligation = table.Column<bool>(type: "bit", nullable: true),
                    ObligationDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DocumentValidationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsLocationCorrect = table.Column<bool>(type: "bit", nullable: true),
                    Distance = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    RoadWidth = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    RightOfWay = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    RoadSurfaceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoadSurfaceTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DecorationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DecorationTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ConstructionYear = table.Column<int>(type: "int", nullable: true),
                    TotalNumberOfFloors = table.Column<int>(type: "int", nullable: true),
                    BuildingFormType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GroundFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GroundFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    UpperFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpperFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BathroomFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BathroomFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RoofType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    RoofTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsExpropriated = table.Column<bool>(type: "bit", nullable: true),
                    ExpropriationRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsInExpropriationLine = table.Column<bool>(type: "bit", nullable: true),
                    RoyalDecree = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsForestBoundary = table.Column<bool>(type: "bit", nullable: true),
                    ForestBoundaryRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ImageDocumentIds = table.Column<string>(type: "nvarchar(2000)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTowers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTowers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "appraisal",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectUnitUploads",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectUnitUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectUnitUploads_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "appraisal",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectLandTitles",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectLandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TitleType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BookNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PageNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LandParcelNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SurveyNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MapSheetNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Rawang = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AerialMapName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AerialMapNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AreaRai = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    AreaNgan = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    AreaSquareWa = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    BoundaryMarkerType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BoundaryMarkerRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DocumentValidationResultType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsMissingFromSurvey = table.Column<bool>(type: "bit", nullable: true),
                    GovernmentPricePerSqWa = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    GovernmentPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectLandTitles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectLandTitles_ProjectLands_ProjectLandId",
                        column: x => x.ProjectLandId,
                        principalSchema: "appraisal",
                        principalTable: "ProjectLands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectModelAreaDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AreaSize = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    ProjectModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectModelAreaDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectModelAreaDetails_ProjectModels_ProjectModelId",
                        column: x => x.ProjectModelId,
                        principalSchema: "appraisal",
                        principalTable: "ProjectModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectModelDepreciationDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Area = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Year = table.Column<short>(type: "smallint", nullable: false),
                    IsBuilding = table.Column<bool>(type: "bit", nullable: false),
                    PricePerSqMBeforeDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriceBeforeDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PricePerSqMAfterDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriceAfterDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DepreciationMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DepreciationYearPct = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    TotalDepreciationPct = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    PriceDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectModelDepreciationDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectModelDepreciationDetails_ProjectModels_ProjectModelId",
                        column: x => x.ProjectModelId,
                        principalSchema: "appraisal",
                        principalTable: "ProjectModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectModelSurfaces",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromFloorNumber = table.Column<int>(type: "int", nullable: false),
                    ToFloorNumber = table.Column<int>(type: "int", nullable: false),
                    FloorType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FloorStructureType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FloorStructureTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FloorSurfaceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FloorSurfaceTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectModelSurfaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectModelSurfaces_ProjectModels_ProjectModelId",
                        column: x => x.ProjectModelId,
                        principalSchema: "appraisal",
                        principalTable: "ProjectModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectModelAssumptions",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ModelDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UsableAreaFrom = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UsableAreaTo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StandardPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StandardLandPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CoverageAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FireInsuranceCondition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProjectPricingAssumptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectModelAssumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectModelAssumptions_ProjectPricingAssumptions_ProjectPricingAssumptionId",
                        column: x => x.ProjectPricingAssumptionId,
                        principalSchema: "appraisal",
                        principalTable: "ProjectPricingAssumptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectUnits",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectTowerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    ModelType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UsableArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Floor = table.Column<int>(type: "int", nullable: true),
                    TowerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CondoRegistrationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoomNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PlotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HouseNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NumberOfFloors = table.Column<int>(type: "int", nullable: true),
                    LandArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectUnits_ProjectModels_ProjectModelId",
                        column: x => x.ProjectModelId,
                        principalSchema: "appraisal",
                        principalTable: "ProjectModels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectUnits_ProjectTowers_ProjectTowerId",
                        column: x => x.ProjectTowerId,
                        principalSchema: "appraisal",
                        principalTable: "ProjectTowers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectUnits_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "appraisal",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectModelDepreciationPeriods",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectModelDepreciationDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtYear = table.Column<int>(type: "int", nullable: false),
                    ToYear = table.Column<int>(type: "int", nullable: false),
                    DepreciationPerYear = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    TotalDepreciationPct = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    PriceDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectModelDepreciationPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectModelDepreciationPeriods_ProjectModelDepreciationDetails_ProjectModelDepreciationDetailId",
                        column: x => x.ProjectModelDepreciationDetailId,
                        principalSchema: "appraisal",
                        principalTable: "ProjectModelDepreciationDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectUnitPrices",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsCorner = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsEdge = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsOther = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsPoolView = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsSouth = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsNearGarden = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LandIncreaseDecreaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AdjustPriceLocation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StandardPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PriceIncrementPerFloor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalAppraisalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalAppraisalValueRounded = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ForceSellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CoverageAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectUnitPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectUnitPrices_ProjectUnits_ProjectUnitId",
                        column: x => x.ProjectUnitId,
                        principalSchema: "appraisal",
                        principalTable: "ProjectUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLands_ProjectId",
                schema: "appraisal",
                table: "ProjectLands",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLandTitles_ProjectLandId",
                schema: "appraisal",
                table: "ProjectLandTitles",
                column: "ProjectLandId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModelAreaDetails_ProjectModelId",
                schema: "appraisal",
                table: "ProjectModelAreaDetails",
                column: "ProjectModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModelAssumptions_ProjectPricingAssumptionId",
                schema: "appraisal",
                table: "ProjectModelAssumptions",
                column: "ProjectPricingAssumptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModelDepreciationDetails_ProjectModelId",
                schema: "appraisal",
                table: "ProjectModelDepreciationDetails",
                column: "ProjectModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModelDepreciationPeriods_ProjectModelDepreciationDetailId",
                schema: "appraisal",
                table: "ProjectModelDepreciationPeriods",
                column: "ProjectModelDepreciationDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModels_ProjectId",
                schema: "appraisal",
                table: "ProjectModels",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModelSurfaces_ProjectModelId",
                schema: "appraisal",
                table: "ProjectModelSurfaces",
                column: "ProjectModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPricingAssumptions_ProjectId",
                schema: "appraisal",
                table: "ProjectPricingAssumptions",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_AppraisalId",
                schema: "appraisal",
                table: "Projects",
                column: "AppraisalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTowers_ProjectId",
                schema: "appraisal",
                table: "ProjectTowers",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitPrices_ProjectUnitId",
                schema: "appraisal",
                table: "ProjectUnitPrices",
                column: "ProjectUnitId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectId",
                schema: "appraisal",
                table: "ProjectUnits",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectId_SequenceNumber",
                schema: "appraisal",
                table: "ProjectUnits",
                columns: new[] { "ProjectId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectModelId",
                schema: "appraisal",
                table: "ProjectUnits",
                column: "ProjectModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_ProjectTowerId",
                schema: "appraisal",
                table: "ProjectUnits",
                column: "ProjectTowerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnits_UploadBatchId",
                schema: "appraisal",
                table: "ProjectUnits",
                column: "UploadBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUnitUploads_ProjectId",
                schema: "appraisal",
                table: "ProjectUnitUploads",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectLandTitles",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ProjectModelAreaDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ProjectModelAssumptions",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ProjectModelDepreciationPeriods",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ProjectModelSurfaces",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ProjectUnitPrices",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ProjectUnitUploads",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ProjectLands",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ProjectPricingAssumptions",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ProjectModelDepreciationDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ProjectUnits",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ProjectModels",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ProjectTowers",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "appraisal");

            migrationBuilder.CreateTable(
                name: "CondoModels",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BathroomFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BathroomFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BuildingNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FireInsuranceCondition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GroundFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GroundFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    HasMezzanine = table.Column<bool>(type: "bit", nullable: true),
                    ImageDocumentIds = table.Column<string>(type: "nvarchar(2000)", nullable: true),
                    ModelDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RoomLayoutType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoomLayoutTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StandardPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StandardUsableArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    StartingPriceMax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StartingPriceMin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpperFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpperFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    UsableAreaMax = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    UsableAreaMin = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondoModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondoModels_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CondoPricingAssumptions",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CornerAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EdgeAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FloorIncrementAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FloorIncrementEveryXFloor = table.Column<int>(type: "int", nullable: true),
                    ForceSalePercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LocationMethod = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OtherAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PoolViewAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SouthAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondoPricingAssumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondoPricingAssumptions_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CondoProjects",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuiltOnTitleDeedNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Developer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Facilities = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    FacilitiesOther = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LandAreaNgan = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    LandAreaRai = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    LandAreaWa = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    LandOffice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LocationNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NumberOfPhase = table.Column<int>(type: "int", nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProjectDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProjectName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProjectSaleLaunchDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProjectType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Road = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UnitForSaleCount = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Utilities = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    UtilitiesOther = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AddressLandOffice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondoProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondoProjects_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CondoTowers",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BathroomFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BathroomFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BuildingFormType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConditionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CondoRegistrationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionYear = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecorationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DecorationTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Distance = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    DocumentValidationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpropriationRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ForestBoundaryRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    GroundFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GroundFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    HasObligation = table.Column<bool>(type: "bit", nullable: true),
                    ImageDocumentIds = table.Column<string>(type: "nvarchar(2000)", nullable: true),
                    IsExpropriated = table.Column<bool>(type: "bit", nullable: true),
                    IsForestBoundary = table.Column<bool>(type: "bit", nullable: true),
                    IsInExpropriationLine = table.Column<bool>(type: "bit", nullable: true),
                    IsLocationCorrect = table.Column<bool>(type: "bit", nullable: true),
                    ModelTypeIds = table.Column<string>(type: "nvarchar(2000)", nullable: true),
                    NumberOfFloors = table.Column<int>(type: "int", nullable: true),
                    NumberOfUnits = table.Column<int>(type: "int", nullable: true),
                    ObligationDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RightOfWay = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    RoadSurfaceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoadSurfaceTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RoadWidth = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    RoofType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    RoofTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RoyalDecree = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TotalNumberOfFloors = table.Column<int>(type: "int", nullable: true),
                    TowerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpperFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpperFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondoTowers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondoTowers_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CondoUnitUploads",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondoUnitUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondoUnitUploads_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageModels",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildingAge = table.Column<int>(type: "int", nullable: true),
                    BuildingMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildingStyleType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildingType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildingTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CeilingType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    CeilingTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ConstructionStyleRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ConstructionStyleType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ConstructionYear = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecorationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DecorationTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EncroachingOthersArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    EncroachingOthersRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ExteriorWallType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    ExteriorWallTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FenceType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    FenceTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FireInsuranceCondition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImageDocumentIds = table.Column<string>(type: "nvarchar(2000)", nullable: true),
                    InteriorWallType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    InteriorWallTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsEncroachingOthers = table.Column<bool>(type: "bit", nullable: true),
                    IsResidential = table.Column<bool>(type: "bit", nullable: true),
                    LandAreaNgan = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    LandAreaRai = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    LandAreaWa = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    ModelDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NumberOfFloors = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: true),
                    NumberOfHouse = table.Column<int>(type: "int", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ResidentialRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RoofFrameType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    RoofFrameTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RoofType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    RoofTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StandardLandArea = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    StandardUsableArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    StartingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StructureType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    StructureTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsableAreaMax = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    UsableAreaMin = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    UtilizationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UtilizationTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageModels_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillagePricingAssumptions",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CornerAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EdgeAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ForceSalePercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandIncreaseDecreaseRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LocationMethod = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NearGardenAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OtherAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillagePricingAssumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillagePricingAssumptions_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageProjectLands",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessRoadWidth = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    AddressLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AllocationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DistanceFromMainRoad = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    EastAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EastBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    ElectricityDistance = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    EncroachmentArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    EncroachmentRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EvictionType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    EvictionTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ExpropriationLineRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ExpropriationRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ForestBoundaryRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    HasBuilding = table.Column<bool>(type: "bit", nullable: true),
                    HasBuildingOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    HasElectricity = table.Column<bool>(type: "bit", nullable: true),
                    HasObligation = table.Column<bool>(type: "bit", nullable: true),
                    IsEncroached = table.Column<bool>(type: "bit", nullable: true),
                    IsExpropriated = table.Column<bool>(type: "bit", nullable: true),
                    IsForestBoundary = table.Column<bool>(type: "bit", nullable: true),
                    IsInExpropriationLine = table.Column<bool>(type: "bit", nullable: true),
                    IsLandLocationVerified = table.Column<bool>(type: "bit", nullable: true),
                    IsLandlocked = table.Column<bool>(type: "bit", nullable: true),
                    IsOwnerVerified = table.Column<bool>(type: "bit", nullable: true),
                    LandAccessibilityRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LandAccessibilityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandCheckMethodType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandCheckMethodTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LandDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LandEntranceExitType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    LandEntranceExitTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LandFillPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LandFillType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandFillTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LandShapeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandUseType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    LandUseTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LandZoneType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    LandZoneTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LandlockedRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NorthAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NorthBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    NumberOfSidesFacingRoad = table.Column<int>(type: "int", nullable: true),
                    ObligationDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OtherLegalLimitations = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PlotLocationType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    PlotLocationTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PondArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PondDepth = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PropertyAnticipationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PropertyAnticipationTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PublicUtilityType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    PublicUtilityTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RightOfWay = table.Column<short>(type: "smallint", nullable: true),
                    RoadFrontage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    RoadPassInFrontOfLand = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoadSurfaceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoadSurfaceTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RoyalDecree = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SoilLevel = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    SouthAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SouthBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TransportationAccessType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    TransportationAccessTypeOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UrbanPlanningType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Village = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WestAdjacentArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WestBoundaryLength = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandOffice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageProjectLands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageProjectLands_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageProjects",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Developer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Facilities = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    FacilitiesOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LandAreaNgan = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    LandAreaRai = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    LandAreaWa = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    LandOffice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LicenseExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LocationNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NumberOfPhase = table.Column<int>(type: "int", nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProjectDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProjectName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProjectSaleLaunchDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProjectType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Road = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UnitForSaleCount = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Utilities = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    UtilitiesOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AddressLandOffice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageProjects_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageUnitUploads",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageUnitUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageUnitUploads_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CondoModelAreaDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AreaSize = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    CondoModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondoModelAreaDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondoModelAreaDetails_CondoModels_CondoModelId",
                        column: x => x.CondoModelId,
                        principalSchema: "appraisal",
                        principalTable: "CondoModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CondoModelAssumptions",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CondoModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CondoPricingAssumptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoverageAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FireInsuranceCondition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ModelDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ModelType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StandardPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UsableAreaFrom = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UsableAreaTo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondoModelAssumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondoModelAssumptions_CondoPricingAssumptions_CondoPricingAssumptionId",
                        column: x => x.CondoPricingAssumptionId,
                        principalSchema: "appraisal",
                        principalTable: "CondoPricingAssumptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CondoUnits",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CondoModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CondoRegistrationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CondoTowerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Floor = table.Column<int>(type: "int", nullable: true),
                    ModelType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoomNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    TowerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsableArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondoUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondoUnits_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CondoUnits_CondoModels_CondoModelId",
                        column: x => x.CondoModelId,
                        principalSchema: "appraisal",
                        principalTable: "CondoModels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CondoUnits_CondoTowers_CondoTowerId",
                        column: x => x.CondoTowerId,
                        principalSchema: "appraisal",
                        principalTable: "CondoTowers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VillageModelAreaDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AreaDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AreaSize = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VillageModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageModelAreaDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageModelAreaDetails_VillageModels_VillageModelId",
                        column: x => x.VillageModelId,
                        principalSchema: "appraisal",
                        principalTable: "VillageModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageModelDepreciationDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Area = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AreaDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DepreciationMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DepreciationYearPct = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    IsBuilding = table.Column<bool>(type: "bit", nullable: false),
                    PriceAfterDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriceBeforeDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriceDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PricePerSqMAfterDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PricePerSqMBeforeDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDepreciationPct = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VillageModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageModelDepreciationDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageModelDepreciationDetails_VillageModels_VillageModelId",
                        column: x => x.VillageModelId,
                        principalSchema: "appraisal",
                        principalTable: "VillageModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageModelSurfaces",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FloorStructureType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FloorStructureTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FloorSurfaceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FloorSurfaceTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FloorType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FromFloorNumber = table.Column<int>(type: "int", nullable: false),
                    ToFloorNumber = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VillageModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageModelSurfaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageModelSurfaces_VillageModels_VillageModelId",
                        column: x => x.VillageModelId,
                        principalSchema: "appraisal",
                        principalTable: "VillageModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageUnits",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HouseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LandArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NumberOfFloors = table.Column<int>(type: "int", nullable: true),
                    PlotNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsableArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    VillageModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageUnits_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalSchema: "appraisal",
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VillageUnits_VillageModels_VillageModelId",
                        column: x => x.VillageModelId,
                        principalSchema: "appraisal",
                        principalTable: "VillageModels",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VillageModelAssumptions",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CoverageAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FireInsuranceCondition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ModelDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ModelType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StandardLandPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StandardPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UsableAreaFrom = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UsableAreaTo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    VillageModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VillagePricingAssumptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageModelAssumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageModelAssumptions_VillagePricingAssumptions_VillagePricingAssumptionId",
                        column: x => x.VillagePricingAssumptionId,
                        principalSchema: "appraisal",
                        principalTable: "VillagePricingAssumptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageProjectLandTitles",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AerialMapName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AerialMapNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BookNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BoundaryMarkerRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BoundaryMarkerType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentValidationResultType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GovernmentPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    GovernmentPricePerSqWa = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsMissingFromSurvey = table.Column<bool>(type: "bit", nullable: true),
                    LandParcelNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MapSheetNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PageNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Rawang = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SurveyNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TitleNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TitleType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VillageProjectLandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaNgan = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    AreaRai = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    AreaSquareWa = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageProjectLandTitles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageProjectLandTitles_VillageProjectLands_VillageProjectLandId",
                        column: x => x.VillageProjectLandId,
                        principalSchema: "appraisal",
                        principalTable: "VillageProjectLands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CondoUnitPrices",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AdjustPriceLocation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CondoUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoverageAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ForceSellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsCorner = table.Column<bool>(type: "bit", nullable: false),
                    IsEdge = table.Column<bool>(type: "bit", nullable: false),
                    IsOther = table.Column<bool>(type: "bit", nullable: false),
                    IsPoolView = table.Column<bool>(type: "bit", nullable: false),
                    IsSouth = table.Column<bool>(type: "bit", nullable: false),
                    PriceIncrementPerFloor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StandardPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalAppraisalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalAppraisalValueRounded = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondoUnitPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondoUnitPrices_CondoUnits_CondoUnitId",
                        column: x => x.CondoUnitId,
                        principalSchema: "appraisal",
                        principalTable: "CondoUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageModelDepreciationPeriods",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AtYear = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DepreciationPerYear = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    PriceDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ToYear = table.Column<int>(type: "int", nullable: false),
                    TotalDepreciationPct = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VillageModelDepreciationDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageModelDepreciationPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageModelDepreciationPeriods_VillageModelDepreciationDetails_VillageModelDepreciationDetailId",
                        column: x => x.VillageModelDepreciationDetailId,
                        principalSchema: "appraisal",
                        principalTable: "VillageModelDepreciationDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageUnitPrices",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AdjustPriceLocation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CoverageAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ForceSellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsCorner = table.Column<bool>(type: "bit", nullable: false),
                    IsEdge = table.Column<bool>(type: "bit", nullable: false),
                    IsNearGarden = table.Column<bool>(type: "bit", nullable: false),
                    IsOther = table.Column<bool>(type: "bit", nullable: false),
                    LandIncreaseDecreaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StandardPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalAppraisalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalAppraisalValueRounded = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VillageUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageUnitPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageUnitPrices_VillageUnits_VillageUnitId",
                        column: x => x.VillageUnitId,
                        principalSchema: "appraisal",
                        principalTable: "VillageUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CondoModelAreaDetails_CondoModelId",
                schema: "appraisal",
                table: "CondoModelAreaDetails",
                column: "CondoModelId");

            migrationBuilder.CreateIndex(
                name: "IX_CondoModelAssumptions_CondoPricingAssumptionId",
                schema: "appraisal",
                table: "CondoModelAssumptions",
                column: "CondoPricingAssumptionId");

            migrationBuilder.CreateIndex(
                name: "IX_CondoModels_AppraisalId",
                schema: "appraisal",
                table: "CondoModels",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_CondoPricingAssumptions_AppraisalId",
                schema: "appraisal",
                table: "CondoPricingAssumptions",
                column: "AppraisalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CondoProjects_AppraisalId",
                schema: "appraisal",
                table: "CondoProjects",
                column: "AppraisalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CondoTowers_AppraisalId",
                schema: "appraisal",
                table: "CondoTowers",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_CondoUnitPrices_CondoUnitId",
                schema: "appraisal",
                table: "CondoUnitPrices",
                column: "CondoUnitId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CondoUnits_AppraisalId",
                schema: "appraisal",
                table: "CondoUnits",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_CondoUnits_AppraisalId_SequenceNumber",
                schema: "appraisal",
                table: "CondoUnits",
                columns: new[] { "AppraisalId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_CondoUnits_CondoModelId",
                schema: "appraisal",
                table: "CondoUnits",
                column: "CondoModelId");

            migrationBuilder.CreateIndex(
                name: "IX_CondoUnits_CondoTowerId",
                schema: "appraisal",
                table: "CondoUnits",
                column: "CondoTowerId");

            migrationBuilder.CreateIndex(
                name: "IX_CondoUnits_UploadBatchId",
                schema: "appraisal",
                table: "CondoUnits",
                column: "UploadBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CondoUnitUploads_AppraisalId",
                schema: "appraisal",
                table: "CondoUnitUploads",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_VillageModelAreaDetails_VillageModelId",
                schema: "appraisal",
                table: "VillageModelAreaDetails",
                column: "VillageModelId");

            migrationBuilder.CreateIndex(
                name: "IX_VillageModelAssumptions_VillagePricingAssumptionId",
                schema: "appraisal",
                table: "VillageModelAssumptions",
                column: "VillagePricingAssumptionId");

            migrationBuilder.CreateIndex(
                name: "IX_VillageModelDepreciationDetails_VillageModelId",
                schema: "appraisal",
                table: "VillageModelDepreciationDetails",
                column: "VillageModelId");

            migrationBuilder.CreateIndex(
                name: "IX_VillageModelDepreciationPeriods_VillageModelDepreciationDetailId",
                schema: "appraisal",
                table: "VillageModelDepreciationPeriods",
                column: "VillageModelDepreciationDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_VillageModels_AppraisalId",
                schema: "appraisal",
                table: "VillageModels",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_VillageModelSurfaces_VillageModelId",
                schema: "appraisal",
                table: "VillageModelSurfaces",
                column: "VillageModelId");

            migrationBuilder.CreateIndex(
                name: "IX_VillagePricingAssumptions_AppraisalId",
                schema: "appraisal",
                table: "VillagePricingAssumptions",
                column: "AppraisalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VillageProjectLands_AppraisalId",
                schema: "appraisal",
                table: "VillageProjectLands",
                column: "AppraisalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VillageProjectLandTitles_VillageProjectLandId",
                schema: "appraisal",
                table: "VillageProjectLandTitles",
                column: "VillageProjectLandId");

            migrationBuilder.CreateIndex(
                name: "IX_VillageProjects_AppraisalId",
                schema: "appraisal",
                table: "VillageProjects",
                column: "AppraisalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VillageUnitPrices_VillageUnitId",
                schema: "appraisal",
                table: "VillageUnitPrices",
                column: "VillageUnitId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VillageUnits_AppraisalId",
                schema: "appraisal",
                table: "VillageUnits",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_VillageUnits_AppraisalId_SequenceNumber",
                schema: "appraisal",
                table: "VillageUnits",
                columns: new[] { "AppraisalId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_VillageUnits_UploadBatchId",
                schema: "appraisal",
                table: "VillageUnits",
                column: "UploadBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_VillageUnits_VillageModelId",
                schema: "appraisal",
                table: "VillageUnits",
                column: "VillageModelId");

            migrationBuilder.CreateIndex(
                name: "IX_VillageUnitUploads_AppraisalId",
                schema: "appraisal",
                table: "VillageUnitUploads",
                column: "AppraisalId");
        }
    }
}
