using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BranchLocks",
                columns: table => new
                {
                    InstanceId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TokenId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    AcquiredUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchLocks", x => new { x.InstanceId, x.TokenId });
                });

            migrationBuilder.CreateTable(
                name: "HistoryEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstanceId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RootInstanceId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Seq = table.Column<int>(type: "int", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NodeId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Event = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tokens",
                columns: table => new
                {
                    InstanceId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TokenId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NodeId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WaitingSignal = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DueUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WaitingTarget = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    WaitingForChildInstanceId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tokens", x => new { x.InstanceId, x.TokenId });
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitions", x => new { x.Id, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "WorkflowInstances",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DefinitionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefinitionVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CorrelationKey = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    VariablesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FaultMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentInstanceId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ParentTokenId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RootInstanceId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CallDepth = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowInstances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BranchLocks_Owner",
                table: "BranchLocks",
                column: "Owner");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEntries_InstanceId",
                table: "HistoryEntries",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEntries_InstanceId_Seq",
                table: "HistoryEntries",
                columns: new[] { "InstanceId", "Seq" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEntries_RootInstanceId",
                table: "HistoryEntries",
                column: "RootInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEntries_Severity",
                table: "HistoryEntries",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_DueUtc",
                table: "Tokens",
                column: "DueUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_InstanceId",
                table: "Tokens",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_Status",
                table: "Tokens",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_WaitingSignal",
                table: "Tokens",
                column: "WaitingSignal");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_WaitingTarget",
                table: "Tokens",
                column: "WaitingTarget");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_TenantId",
                table: "WorkflowDefinitions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_CorrelationKey",
                table: "WorkflowInstances",
                column: "CorrelationKey");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_ParentInstanceId",
                table: "WorkflowInstances",
                column: "ParentInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_RootInstanceId",
                table: "WorkflowInstances",
                column: "RootInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_Status",
                table: "WorkflowInstances",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_TenantId",
                table: "WorkflowInstances",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchLocks");

            migrationBuilder.DropTable(
                name: "HistoryEntries");

            migrationBuilder.DropTable(
                name: "Tokens");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitions");

            migrationBuilder.DropTable(
                name: "WorkflowInstances");
        }
    }
}
