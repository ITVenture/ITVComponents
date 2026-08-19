using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class SuspendAndFaultCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_Status_Priority",
                table: "WorkflowInstances");

            migrationBuilder.AddColumn<string>(
                name: "FaultCode",
                table: "WorkflowInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Suspended",
                table: "WorkflowInstances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SuspendedReason",
                table: "WorkflowInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_Status_Suspended_Priority",
                table: "WorkflowInstances",
                columns: new[] { "Status", "Suspended", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_Status_Suspended_Priority",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "FaultCode",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "Suspended",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "SuspendedReason",
                table: "WorkflowInstances");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_Status_Priority",
                table: "WorkflowInstances",
                columns: new[] { "Status", "Priority" });
        }
    }
}
