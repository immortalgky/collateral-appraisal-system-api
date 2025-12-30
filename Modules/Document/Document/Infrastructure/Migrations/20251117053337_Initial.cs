using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "document");

            migrationBuilder.CreateTable(
                name: "UploadSessions",
                schema: "document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalDocuments = table.Column<int>(type: "int", nullable: false),
                    TotalSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UploadSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DocumentCategory = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileExtension = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StorageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UploadedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReferenceCount = table.Column<int>(type: "int", nullable: false),
                    LastLinkedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUnlinkedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsOrphaned = table.Column<bool>(type: "bit", nullable: false),
                    OrphanedReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AccessLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchivedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ArchivedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomMetadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Checksum = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChecksumAlgorithm = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_UploadSessions_UploadSessionId",
                        column: x => x.UploadSessionId,
                        principalSchema: "document",
                        principalTable: "UploadSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentCategory",
                schema: "document",
                table: "Documents",
                column: "DocumentCategory",
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentNumber",
                schema: "document",
                table: "Documents",
                column: "DocumentNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentType",
                schema: "document",
                table: "Documents",
                column: "DocumentType",
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadSessionId",
                schema: "document",
                table: "Documents",
                column: "UploadSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadSessions_Status",
                schema: "document",
                table: "UploadSessions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Documents",
                schema: "document");

            migrationBuilder.DropTable(
                name: "UploadSessions",
                schema: "document");
        }
    }
}
