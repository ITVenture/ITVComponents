using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InstancePriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "WorkflowInstances",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_Status_Priority",
                table: "WorkflowInstances",
                columns: new[] { "Status", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_Status_Priority",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "WorkflowInstances");
        }
    }
}
