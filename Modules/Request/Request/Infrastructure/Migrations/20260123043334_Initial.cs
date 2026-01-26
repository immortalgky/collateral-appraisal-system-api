using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "request");

            migrationBuilder.CreateTable(
                name: "InboxState",
                schema: "request",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Received = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceiveCount = table.Column<int>(type: "int", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Consumed = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxState", x => x.Id);
                    table.UniqueConstraint("AK_InboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                schema: "request",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
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
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
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
                    TitleNo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeedType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CondoName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildingNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoomNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FloorNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
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
                    LandLocationInfo_BookNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LandLocationInfo_PageNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LandNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SurveyNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LandLocationInfo_MapSheetNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LandLocationInfo_AerialMapName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LandLocationInfo_AerialMapNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegistrationNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MachineType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    InstallationStatus = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NumberOfMachinery = table.Column<int>(type: "int", nullable: true),
                    VehicleType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    VehicleLocation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    VIN = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LicensePlateNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VesselType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    VesselLocation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    HullIdentificationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VesselRegistrationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestTitles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "request",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnqueueTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InboxConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DestinationAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ResponseAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FaultAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                        columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                        principalSchema: "request",
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalSchema: "request",
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
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
                    LoanApplicationNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FacilityLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    AdditionalFacilityLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PreviousFacilityLimit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    TotalSellingPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PrevAppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    ContactPersonPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DealerCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AppointmentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppointmentLocation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FeePaymentType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AbsorbedFee = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
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
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UploadedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
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
                name: "IX_InboxState_Delivered",
                schema: "request",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                schema: "request",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                schema: "request",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                schema: "request",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true,
                filter: "[InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                schema: "request",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true,
                filter: "[OutboxId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                schema: "request",
                table: "OutboxState",
                column: "Created");

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
                column: "TitleNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessage",
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
                name: "InboxState",
                schema: "request");

            migrationBuilder.DropTable(
                name: "OutboxState",
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
