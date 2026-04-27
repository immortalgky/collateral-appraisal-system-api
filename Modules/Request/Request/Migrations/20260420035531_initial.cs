using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Migrations
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
                name: "InboxMessage",
                schema: "request",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumerType = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessage", x => new { x.MessageId, x.ConsumerType });
                });

            migrationBuilder.CreateTable(
                name: "IntegrationEventOutbox",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationEventOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxDeliveryLock",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InstanceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LeasedUntil = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcquiredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxDeliveryLock", x => x.Id);
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
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestComments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Requests",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestNumber = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Requestor = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RequestorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Creator = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsPma = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ExternalCaseKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExternalSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
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
                    OwnerName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HouseNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ProjectName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Moo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Road = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    District = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DopaHouseNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DopaProjectName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DopaMoo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DopaSoi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DopaRoad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DopaSubDistrict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DopaDistrict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DopaProvince = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DopaPostcode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BuildingType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UsableArea = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    NumberOfBuilding = table.Column<int>(type: "int", nullable: true),
                    TitleNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TitleType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CondoName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoomNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FloorNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    BookNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PageNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LandParcelNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SurveyNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MapSheetNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Rawang = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AerialMapName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AerialMapNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AreaRai = table.Column<int>(type: "int", nullable: true),
                    AreaNgan = table.Column<int>(type: "int", nullable: true),
                    AreaSquareWa = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MachineType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    InstallationStatus = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NumberOfMachine = table.Column<int>(type: "int", nullable: true),
                    VehicleType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    VehicleLocation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    VIN = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LicensePlateNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VesselType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    VesselLocation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    HIN = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VesselRegistrationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestTitles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestCustomers",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ContactNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    LoanApplicationNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FacilityLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    AdditionalFacilityLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PreviousFacilityLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    TotalSellingPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PrevAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrevAppraisalNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PrevAppraisalValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PrevAppraisalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HouseNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ProjectName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Moo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Soi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Road = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SubDistrict = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    District = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ContactPersonName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactPersonPhone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DealerCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AppointmentDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppointmentLocation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FeePaymentType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AbsorbedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    FeeNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
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
                    DocumentType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Prefix = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Set = table.Column<short>(type: "smallint", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UploadedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                name: "RequestTitleDocuments",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Prefix = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Set = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UploadedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestTitleDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestTitleDocuments_RequestTitles_TitleId",
                        column: x => x.TitleId,
                        principalSchema: "request",
                        principalTable: "RequestTitles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessage_Cleanup",
                schema: "request",
                table: "InboxMessage",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessage_StaleProcessing",
                schema: "request",
                table: "InboxMessage",
                columns: new[] { "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEventOutbox_Cleanup",
                schema: "request",
                table: "IntegrationEventOutbox",
                columns: new[] { "Status", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEventOutbox_Correlation",
                schema: "request",
                table: "IntegrationEventOutbox",
                columns: new[] { "CorrelationId", "Status", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEventOutbox_DeadLetter",
                schema: "request",
                table: "IntegrationEventOutbox",
                columns: new[] { "Status", "RetryCount" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEventOutbox_Polling",
                schema: "request",
                table: "IntegrationEventOutbox",
                columns: new[] { "Status", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestComments_RequestId",
                schema: "request",
                table: "RequestComments",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestCustomer_Name",
                schema: "request",
                table: "RequestCustomers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_RequestCustomers_RequestId",
                schema: "request",
                table: "RequestCustomers",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Request_LoanApplicationNumber",
                schema: "request",
                table: "RequestDetails",
                column: "LoanApplicationNumber",
                filter: "[LoanApplicationNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RequestDocument_Request_Document",
                schema: "request",
                table: "RequestDocuments",
                columns: new[] { "RequestId", "DocumentId" },
                unique: true,
                filter: "[DocumentId] IS NOT NULL");

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
                name: "IX_Request_ExternalCaseKey",
                schema: "request",
                table: "Requests",
                column: "ExternalCaseKey",
                filter: "[ExternalCaseKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Request_RequestedAt",
                schema: "request",
                table: "Requests",
                column: "RequestedAt",
                descending: new bool[0],
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Request_RequestNumber",
                schema: "request",
                table: "Requests",
                column: "RequestNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Request_Requestor",
                schema: "request",
                table: "Requests",
                column: "Requestor",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Request_Status",
                schema: "request",
                table: "Requests",
                column: "Status",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TitleDocument_Title_Document",
                schema: "request",
                table: "RequestTitleDocuments",
                columns: new[] { "TitleId", "DocumentId" },
                unique: true,
                filter: "[DocumentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TitleDeedInfo_RequestId",
                schema: "request",
                table: "RequestTitles",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TitleDeedInfo_TitleDeedNumber",
                schema: "request",
                table: "RequestTitles",
                column: "TitleNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessage",
                schema: "request");

            migrationBuilder.DropTable(
                name: "IntegrationEventOutbox",
                schema: "request");

            migrationBuilder.DropTable(
                name: "OutboxDeliveryLock",
                schema: "request");

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
                name: "Requests",
                schema: "request");

            migrationBuilder.DropTable(
                name: "RequestTitles",
                schema: "request");
        }
    }
}
