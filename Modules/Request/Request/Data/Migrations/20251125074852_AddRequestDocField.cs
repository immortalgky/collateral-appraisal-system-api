using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestDocField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UploadedByName",
                schema: "request",
                table: "RequestDocuments",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<long>(
                name: "UploadedBy",
                schema: "request",
                table: "RequestDocuments",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UploadedAt",
                schema: "request",
                table: "RequestDocuments",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                schema: "request",
                table: "RequestDocuments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<bool>(
                name: "DocumentFollowUp",
                schema: "request",
                table: "RequestDocuments",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                schema: "request",
                table: "RequestDocuments",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                schema: "request",
                table: "RequestDocuments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Prefix",
                schema: "request",
                table: "RequestDocuments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "Set",
                schema: "request",
                table: "RequestDocuments",
                type: "smallint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentFollowUp",
                schema: "request",
                table: "RequestDocuments");

            migrationBuilder.DropColumn(
                name: "FileName",
                schema: "request",
                table: "RequestDocuments");

            migrationBuilder.DropColumn(
                name: "FilePath",
                schema: "request",
                table: "RequestDocuments");

            migrationBuilder.DropColumn(
                name: "Prefix",
                schema: "request",
                table: "RequestDocuments");

            migrationBuilder.DropColumn(
                name: "Set",
                schema: "request",
                table: "RequestDocuments");

            migrationBuilder.AlterColumn<string>(
                name: "UploadedByName",
                schema: "request",
                table: "RequestDocuments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "UploadedBy",
                schema: "request",
                table: "RequestDocuments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UploadedAt",
                schema: "request",
                table: "RequestDocuments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                schema: "request",
                table: "RequestDocuments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
