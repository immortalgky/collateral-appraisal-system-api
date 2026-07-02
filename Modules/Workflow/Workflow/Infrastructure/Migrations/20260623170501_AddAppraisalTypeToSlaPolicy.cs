using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalTypeToSlaPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_Activity",
                schema: "workflow",
                table: "SlaPolicies");

            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_Stage_Start",
                schema: "workflow",
                table: "SlaPolicies");

            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_Workflow",
                schema: "workflow",
                table: "SlaPolicies");

            migrationBuilder.AddColumn<string>(
                name: "AppraisalType",
                schema: "workflow",
                table: "SlaPolicies",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_Activity",
                schema: "workflow",
                table: "SlaPolicies",
                columns: new[] { "ActivityId", "WorkflowDefinitionId", "CompanyId", "LoanType", "AppraisalType", "Priority" },
                unique: true,
                filter: "[Scope] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_Stage_Start",
                schema: "workflow",
                table: "SlaPolicies",
                columns: new[] { "StartActivityKey", "WorkflowDefinitionId", "CompanyId", "LoanType", "AppraisalType", "Priority" },
                unique: true,
                filter: "[Scope] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_Workflow",
                schema: "workflow",
                table: "SlaPolicies",
                columns: new[] { "WorkflowDefinitionId", "LoanType", "AppraisalType" },
                unique: true,
                filter: "[Scope] = 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_Activity",
                schema: "workflow",
                table: "SlaPolicies");

            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_Stage_Start",
                schema: "workflow",
                table: "SlaPolicies");

            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_Workflow",
                schema: "workflow",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "AppraisalType",
                schema: "workflow",
                table: "SlaPolicies");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_Activity",
                schema: "workflow",
                table: "SlaPolicies",
                columns: new[] { "ActivityId", "WorkflowDefinitionId", "CompanyId", "LoanType", "Priority" },
                unique: true,
                filter: "[Scope] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_Stage_Start",
                schema: "workflow",
                table: "SlaPolicies",
                columns: new[] { "StartActivityKey", "WorkflowDefinitionId", "CompanyId", "LoanType", "Priority" },
                unique: true,
                filter: "[Scope] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_Workflow",
                schema: "workflow",
                table: "SlaPolicies",
                columns: new[] { "WorkflowDefinitionId", "LoanType" },
                unique: true,
                filter: "[Scope] = 3");
        }
    }
}
