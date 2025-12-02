using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Data.Migrations
{
    /// <inheritdoc />
    public partial class initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "request");

            migrationBuilder.CreateTable(
                name: "Requests",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestBy = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RequestByName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Creator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPMA = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestComments",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CommentedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CommentedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CommentedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestComments_Requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "request",
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestCustomers",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ContactNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestCustomers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestCustomers_Requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "request",
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestDetails",
                schema: "request",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HasAppraisalBook = table.Column<bool>(type: "bit", nullable: false),
                    BankingSegment = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LoanApplicationNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FacilityLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    AdditionalFacilityLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PreviousFacilityLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    TotalSellingPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PrevAppraisalNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    HouseNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RoomNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FloorNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LocationIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Moo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Road = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    District = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AppointmentDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppointmentLocation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FeeType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FeeRemark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BankAbsorbAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestDetails", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_RequestDetails_Requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "request",
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestDocuments",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Prefix = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Set = table.Column<short>(type: "smallint", nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DocumentFollowUp = table.Column<bool>(type: "bit", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsRequire = table.Column<bool>(type: "bit", nullable: false),
                    UploadedBy = table.Column<long>(type: "bigint", nullable: true),
                    UploadedByName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DocumentDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestDocuments_Requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "request",
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestProperties",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    BuildingType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestProperties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestProperties_Requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "request",
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestTitles",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollateralType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CollateralStatus = table.Column<bool>(type: "bit", nullable: true),
                    TitleNo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeedType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TitleDetail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Rawang = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SurveyNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AreaRai = table.Column<int>(type: "int", nullable: true),
                    AreaNgan = table.Column<int>(type: "int", nullable: true),
                    AreaSquareWa = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RegistrationNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VehicleType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    VehicleAppointmentLocation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ChassisNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MachineStatus = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    MachineType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    InstallationStatus = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NumberOfMachinery = table.Column<int>(type: "int", nullable: true),
                    BuildingType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UsableArea = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    NumberOfBuilding = table.Column<int>(type: "int", nullable: true),
                    CondoName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildingNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoomNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FloorNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    HouseNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ProjectName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Moo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Road = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    District = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DopaHouseNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DopaMoo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DopaSoi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DopaRoad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DopaSubDistrict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DopaDistrict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DopaProvince = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DopaPostcode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestTitles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestTitles_Requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "request",
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestTitleDocuments",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Filename = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Prefix = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Set = table.Column<int>(type: "int", nullable: false),
                    DocumentDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UploadedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestTitleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestTitleDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestTitleDocuments_RequestTitles_RequestTitleId",
                        column: x => x.RequestTitleId,
                        principalSchema: "request",
                        principalTable: "RequestTitles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestComments_RequestId",
                schema: "request",
                table: "RequestComments",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestCustomer_Name",
                schema: "request",
                table: "RequestCustomers",
                column: "CustomerName");

            migrationBuilder.CreateIndex(
                name: "IX_RequestCustomers_RequestId",
                schema: "request",
                table: "RequestCustomers",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Request_LoanApplicationNumber",
                schema: "request",
                table: "RequestDetails",
                column: "LoanApplicationNo",
                filter: "[LoanApplicationNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RequestDocuments_RequestId",
                schema: "request",
                table: "RequestDocuments",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestProperties_RequestId",
                schema: "request",
                table: "RequestProperties",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestProperty_PropertyType",
                schema: "request",
                table: "RequestProperties",
                column: "PropertyType");

            migrationBuilder.CreateIndex(
                name: "IX_Request_RequestDate",
                schema: "request",
                table: "Requests",
                column: "RequestDate",
                descending: new bool[0],
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Request_RequestedBy",
                schema: "request",
                table: "Requests",
                column: "RequestBy",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Request_RequestNumber",
                schema: "request",
                table: "Requests",
                column: "RequestNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Request_Status",
                schema: "request",
                table: "Requests",
                column: "Status",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTitleDocuments_RequestTitleId",
                schema: "request",
                table: "RequestTitleDocuments",
                column: "RequestTitleId");

            migrationBuilder.CreateIndex(
                name: "IX_TitleDeedInfo_RequestId",
                schema: "request",
                table: "RequestTitles",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TitleDeedInfo_TitleDeedNumber",
                schema: "request",
                table: "RequestTitles",
                column: "TitleNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestComments",
                schema: "request");

            migrationBuilder.DropTable(
                name: "RequestCustomers",
                schema: "request");

            migrationBuilder.DropTable(
                name: "RequestDetails",
                schema: "request");

            migrationBuilder.DropTable(
                name: "RequestDocuments",
                schema: "request");

            migrationBuilder.DropTable(
                name: "RequestProperties",
                schema: "request");

            migrationBuilder.DropTable(
                name: "RequestTitleDocuments",
                schema: "request");

            migrationBuilder.DropTable(
                name: "RequestTitles",
                schema: "request");

            migrationBuilder.DropTable(
                name: "Requests",
                schema: "request");
        }
    }
}
