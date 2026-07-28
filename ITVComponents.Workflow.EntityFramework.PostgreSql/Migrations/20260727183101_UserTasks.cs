using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class UserTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedTo",
                table: "Tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimedBy",
                table: "Tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedUntil",
                table: "Tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TaskCreatedUtc",
                table: "Tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TaskDueUtc",
                table: "Tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaskKey",
                table: "Tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaskPermission",
                table: "Tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaskTitle",
                table: "Tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Tokens",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_AssignedTo",
                table: "Tokens",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_TenantId_TaskKey_Status",
                table: "Tokens",
                columns: new[] { "TenantId", "TaskKey", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tokens_AssignedTo",
                table: "Tokens");

            migrationBuilder.DropIndex(
                name: "IX_Tokens_TenantId_TaskKey_Status",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "AssignedTo",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "ClaimedBy",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "ClaimedUntil",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "TaskCreatedUtc",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "TaskDueUtc",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "TaskKey",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "TaskPermission",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "TaskTitle",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Tokens");
        }
    }
}
