using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationSharedDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuotationSharedDocuments",
                schema: "appraisal",
                columns: table => new
                {
                    QuotationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SharedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SharedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationSharedDocuments", x => new { x.QuotationRequestId, x.DocumentId });
                    table.ForeignKey(
                        name: "FK_QuotationSharedDocuments_QuotationRequests_QuotationRequestId",
                        column: x => x.QuotationRequestId,
                        principalSchema: "appraisal",
                        principalTable: "QuotationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationSharedDocuments_QuotationRequestId",
                schema: "appraisal",
                table: "QuotationSharedDocuments",
                column: "QuotationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationSharedDocuments_QuotationRequestId_AppraisalId",
                schema: "appraisal",
                table: "QuotationSharedDocuments",
                columns: new[] { "QuotationRequestId", "AppraisalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuotationSharedDocuments",
                schema: "appraisal");
        }
    }
}
