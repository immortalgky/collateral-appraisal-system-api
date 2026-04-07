using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockCondoEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CondoModels",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ModelDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BuildingNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StartingPriceMin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StartingPriceMax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    HasMezzanine = table.Column<bool>(type: "bit", nullable: true),
                    UsableAreaMin = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    UsableAreaMax = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    StandardUsableArea = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    FireInsuranceCondition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoomLayoutType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoomLayoutTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GroundFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GroundFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpperFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpperFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BathroomFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BathroomFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImageDocumentIds = table.Column<string>(type: "nvarchar(2000)", nullable: true),
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationMethod = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CornerAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EdgeAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PoolViewAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SouthAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OtherAdjustment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FloorIncrementEveryXFloor = table.Column<int>(type: "int", nullable: true),
                    FloorIncrementAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
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
                    BuiltOnTitleDeedNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    RoadSurfaceTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DecorationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DecorationTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConstructionYear = table.Column<int>(type: "int", nullable: true),
                    TotalNumberOfFloors = table.Column<int>(type: "int", nullable: true),
                    BuildingFormType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConstructionMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GroundFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GroundFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpperFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpperFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BathroomFloorMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BathroomFloorMaterialTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoofType = table.Column<string>(type: "nvarchar(500)", nullable: true),
                    RoofTypeOther = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsExpropriated = table.Column<bool>(type: "bit", nullable: true),
                    ExpropriationRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsInExpropriationLine = table.Column<bool>(type: "bit", nullable: true),
                    RoyalDecree = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsForestBoundary = table.Column<bool>(type: "bit", nullable: true),
                    ForestBoundaryRemark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                name: "CondoUnits",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    Floor = table.Column<int>(type: "int", nullable: true),
                    TowerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CondoRegistrationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoomNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ModelType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_CondoUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondoUnits_Appraisals_AppraisalId",
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
                name: "CondoModelAreaDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
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
                    ModelType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ModelDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UsableAreaFrom = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UsableAreaTo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StandardPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CoverageAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FireInsuranceCondition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CondoPricingAssumptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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
                name: "CondoUnitPrices",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CondoUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsCorner = table.Column<bool>(type: "bit", nullable: false),
                    IsEdge = table.Column<bool>(type: "bit", nullable: false),
                    IsPoolView = table.Column<bool>(type: "bit", nullable: false),
                    IsSouth = table.Column<bool>(type: "bit", nullable: false),
                    IsOther = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_CondoUnitPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondoUnitPrices_CondoUnits_CondoUnitId",
                        column: x => x.CondoUnitId,
                        principalSchema: "appraisal",
                        principalTable: "CondoUnits",
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
                name: "IX_CondoUnits_UploadBatchId",
                schema: "appraisal",
                table: "CondoUnits",
                column: "UploadBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CondoUnitUploads_AppraisalId",
                schema: "appraisal",
                table: "CondoUnitUploads",
                column: "AppraisalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "CondoTowers",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CondoUnitPrices",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CondoUnitUploads",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CondoModels",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CondoPricingAssumptions",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "CondoUnits",
                schema: "appraisal");
        }
    }
}
