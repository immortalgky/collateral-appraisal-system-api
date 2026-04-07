using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockVillage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VillageModels",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ModelDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NumberOfHouse = table.Column<int>(type: "int", nullable: true),
                    StartingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UsableAreaMin = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    UsableAreaMax = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    StandardUsableArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    LandAreaRai = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    LandAreaNgan = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    LandAreaWa = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    StandardLandArea = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                    FireInsuranceCondition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImageDocumentIds = table.Column<string>(type: "nvarchar(2000)", nullable: true),
                    BuildingType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildingTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NumberOfFloors = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: true),
                    DecorationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DecorationTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsEncroachingOthers = table.Column<bool>(type: "bit", nullable: true),
                    EncroachingOthersRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EncroachingOthersArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    BuildingMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildingStyleType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsResidential = table.Column<bool>(type: "bit", nullable: true),
                    BuildingAge = table.Column<int>(type: "int", nullable: true),
                    ConstructionYear = table.Column<int>(type: "int", nullable: true),
                    ResidentialRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConstructionStyleType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionStyleRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StructureType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    StructureTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoofFrameType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    RoofFrameTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoofType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    RoofTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CeilingType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    CeilingTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InteriorWallType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    InteriorWallTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExteriorWallType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    ExteriorWallTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FenceType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    FenceTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConstructionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UtilizationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UtilizationTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationMethod = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CornerAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EdgeAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NearGardenAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OtherAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LandIncreaseDecreaseRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ForceSalePercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    LandCheckMethodTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DistanceFromMainRoad = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Village = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LandShapeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UrbanPlanningType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandZoneType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    LandZoneTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PlotLocationType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    PlotLocationTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandFillType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandFillTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandFillPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    SoilLevel = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    AccessRoadWidth = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    RightOfWay = table.Column<short>(type: "smallint", nullable: true),
                    RoadFrontage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    NumberOfSidesFacingRoad = table.Column<int>(type: "int", nullable: true),
                    RoadPassInFrontOfLand = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandAccessibilityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandAccessibilityRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RoadSurfaceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoadSurfaceTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HasElectricity = table.Column<bool>(type: "bit", nullable: true),
                    ElectricityDistance = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PublicUtilityType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    PublicUtilityTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandUseType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    LandUseTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandEntranceExitType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    LandEntranceExitTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TransportationAccessType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    TransportationAccessTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PropertyAnticipationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PropertyAnticipationTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsExpropriated = table.Column<bool>(type: "bit", nullable: true),
                    ExpropriationRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsInExpropriationLine = table.Column<bool>(type: "bit", nullable: true),
                    ExpropriationLineRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RoyalDecree = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsEncroached = table.Column<bool>(type: "bit", nullable: true),
                    EncroachmentRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EncroachmentArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    IsLandlocked = table.Column<bool>(type: "bit", nullable: true),
                    LandlockedRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsForestBoundary = table.Column<bool>(type: "bit", nullable: true),
                    ForestBoundaryRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OtherLegalLimitations = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EvictionType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    EvictionTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    HasBuildingOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    ProjectType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LicenseExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    Remark = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                name: "VillageUnits",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    PlotNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HouseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NumberOfFloors = table.Column<int>(type: "int", nullable: true),
                    LandArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    UsableArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                });

            migrationBuilder.CreateTable(
                name: "VillageUnitUploads",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                name: "VillageModelAreaDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AreaDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AreaSize = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    VillageModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                    VillageModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    VillageModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                name: "VillageModelAssumptions",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    VillageModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ModelDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UsableAreaFrom = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UsableAreaTo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StandardLandPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StandardPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CoverageAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FireInsuranceCondition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    VillageProjectLandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    Remark = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                name: "VillageUnitPrices",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    VillageUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsCorner = table.Column<bool>(type: "bit", nullable: false),
                    IsEdge = table.Column<bool>(type: "bit", nullable: false),
                    IsNearGarden = table.Column<bool>(type: "bit", nullable: false),
                    IsOther = table.Column<bool>(type: "bit", nullable: false),
                    LandIncreaseDecreaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AdjustPriceLocation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StandardPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
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
                    table.PrimaryKey("PK_VillageUnitPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageUnitPrices_VillageUnits_VillageUnitId",
                        column: x => x.VillageUnitId,
                        principalSchema: "appraisal",
                        principalTable: "VillageUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageModelDepreciationPeriods",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    VillageModelDepreciationDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_VillageModelDepreciationPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageModelDepreciationPeriods_VillageModelDepreciationDetails_VillageModelDepreciationDetailId",
                        column: x => x.VillageModelDepreciationDetailId,
                        principalSchema: "appraisal",
                        principalTable: "VillageModelDepreciationDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_VillageUnitUploads_AppraisalId",
                schema: "appraisal",
                table: "VillageUnitUploads",
                column: "AppraisalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "VillageModels",
                schema: "appraisal");
        }
    }
}
