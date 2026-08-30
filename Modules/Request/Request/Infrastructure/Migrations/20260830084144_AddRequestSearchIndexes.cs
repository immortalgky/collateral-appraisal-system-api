using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RequestTitle_CondoName",
                schema: "request",
                table: "RequestTitles",
                column: "CondoName",
                filter: "[CondoName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTitle_LandParcelNumber",
                schema: "request",
                table: "RequestTitles",
                column: "LandParcelNumber",
                filter: "[LandParcelNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTitle_LicensePlateNumber",
                schema: "request",
                table: "RequestTitles",
                column: "LicensePlateNumber",
                filter: "[LicensePlateNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTitle_OwnerName",
                schema: "request",
                table: "RequestTitles",
                column: "OwnerName",
                filter: "[OwnerName] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "RequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestTitle_ProjectName",
                schema: "request",
                table: "RequestTitles",
                column: "ProjectName",
                filter: "[ProjectName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTitle_RoomNumber",
                schema: "request",
                table: "RequestTitles",
                column: "RoomNumber",
                filter: "[RoomNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Request_RequestorName",
                schema: "request",
                table: "Requests",
                column: "RequestorName",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Request_ContactPersonName",
                schema: "request",
                table: "RequestDetails",
                column: "ContactPersonName",
                filter: "[ContactPersonName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Request_ContactPersonPhone",
                schema: "request",
                table: "RequestDetails",
                column: "ContactPersonPhone",
                filter: "[ContactPersonPhone] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Request_PrevAppraisalNumber",
                schema: "request",
                table: "RequestDetails",
                column: "PrevAppraisalNumber",
                filter: "[PrevAppraisalNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RequestCustomer_ContactNumber",
                schema: "request",
                table: "RequestCustomers",
                column: "ContactNumber",
                filter: "[ContactNumber] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "RequestId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RequestTitle_CondoName",
                schema: "request",
                table: "RequestTitles");

            migrationBuilder.DropIndex(
                name: "IX_RequestTitle_LandParcelNumber",
                schema: "request",
                table: "RequestTitles");

            migrationBuilder.DropIndex(
                name: "IX_RequestTitle_LicensePlateNumber",
                schema: "request",
                table: "RequestTitles");

            migrationBuilder.DropIndex(
                name: "IX_RequestTitle_OwnerName",
                schema: "request",
                table: "RequestTitles");

            migrationBuilder.DropIndex(
                name: "IX_RequestTitle_ProjectName",
                schema: "request",
                table: "RequestTitles");

            migrationBuilder.DropIndex(
                name: "IX_RequestTitle_RoomNumber",
                schema: "request",
                table: "RequestTitles");

            migrationBuilder.DropIndex(
                name: "IX_Request_RequestorName",
                schema: "request",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Request_ContactPersonName",
                schema: "request",
                table: "RequestDetails");

            migrationBuilder.DropIndex(
                name: "IX_Request_ContactPersonPhone",
                schema: "request",
                table: "RequestDetails");

            migrationBuilder.DropIndex(
                name: "IX_Request_PrevAppraisalNumber",
                schema: "request",
                table: "RequestDetails");

            migrationBuilder.DropIndex(
                name: "IX_RequestCustomer_ContactNumber",
                schema: "request",
                table: "RequestCustomers");
        }
    }
}
