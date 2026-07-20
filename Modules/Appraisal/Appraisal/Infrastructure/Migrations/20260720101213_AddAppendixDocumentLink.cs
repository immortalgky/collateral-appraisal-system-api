using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppendixDocumentLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "GalleryPhotoId",
                schema: "appraisal",
                table: "AppendixDocuments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId",
                schema: "appraisal",
                table: "AppendixDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppendixDocuments_DocumentId",
                schema: "appraisal",
                table: "AppendixDocuments",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppendixDocuments_DocumentId",
                schema: "appraisal",
                table: "AppendixDocuments");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                schema: "appraisal",
                table: "AppendixDocuments");

            migrationBuilder.AlterColumn<Guid>(
                name: "GalleryPhotoId",
                schema: "appraisal",
                table: "AppendixDocuments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
