using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.PostgreSql.Migrations
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
                    InstanceId = table.Column<string>(type: "text", nullable: false),
                    TokenId = table.Column<string>(type: "text", nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: true),
                    AcquiredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstanceId = table.Column<string>(type: "text", nullable: true),
                    RootInstanceId = table.Column<string>(type: "text", nullable: true),
                    Seq = table.Column<int>(type: "integer", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NodeId = table.Column<string>(type: "text", nullable: true),
                    Event = table.Column<string>(type: "text", nullable: true),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    Severity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tokens",
                columns: table => new
                {
                    InstanceId = table.Column<string>(type: "text", nullable: false),
                    TokenId = table.Column<string>(type: "text", nullable: false),
                    NodeId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WaitingSignal = table.Column<string>(type: "text", nullable: true),
                    DueUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WaitingTarget = table.Column<string>(type: "text", nullable: true),
                    WaitingForChildInstanceId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tokens", x => new { x.InstanceId, x.TokenId });
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    DefinitionJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitions", x => new { x.Id, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "WorkflowInstances",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DefinitionId = table.Column<string>(type: "text", nullable: true),
                    DefinitionVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CorrelationKey = table.Column<string>(type: "text", nullable: true),
                    VariablesJson = table.Column<string>(type: "text", nullable: true),
                    FaultMessage = table.Column<string>(type: "text", nullable: true),
                    ParentInstanceId = table.Column<string>(type: "text", nullable: true),
                    ParentTokenId = table.Column<string>(type: "text", nullable: true),
                    RootInstanceId = table.Column<string>(type: "text", nullable: true),
                    CallDepth = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
