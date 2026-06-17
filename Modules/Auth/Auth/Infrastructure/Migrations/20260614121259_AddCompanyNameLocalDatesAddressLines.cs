using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyNameLocalDatesAddressLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                schema: "auth",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                schema: "auth",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Province",
                schema: "auth",
                table: "Companies");

            migrationBuilder.RenameColumn(
                name: "Street",
                schema: "auth",
                table: "Companies",
                newName: "AddressLine2");

            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                schema: "auth",
                table: "Companies",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveDate",
                schema: "auth",
                table: "Companies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpireDate",
                schema: "auth",
                table: "Companies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameLocal",
                schema: "auth",
                table: "Companies",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressLine1",
                schema: "auth",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "EffectiveDate",
                schema: "auth",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ExpireDate",
                schema: "auth",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "NameLocal",
                schema: "auth",
                table: "Companies");

            migrationBuilder.RenameColumn(
                name: "AddressLine2",
                schema: "auth",
                table: "Companies",
                newName: "Street");

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "auth",
                table: "Companies",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                schema: "auth",
                table: "Companies",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Province",
                schema: "auth",
                table: "Companies",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
