using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BytesPurgedUtc",
                table: "WorkflowAttachments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttachmentCount",
                table: "WorkflowArchivedInstances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowAttachments_BytesPurgedUtc",
                table: "WorkflowAttachments",
                column: "BytesPurgedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowAttachments_BytesPurgedUtc",
                table: "WorkflowAttachments");

            migrationBuilder.DropColumn(
                name: "BytesPurgedUtc",
                table: "WorkflowAttachments");

            migrationBuilder.DropColumn(
                name: "AttachmentCount",
                table: "WorkflowArchivedInstances");
        }
    }
}
