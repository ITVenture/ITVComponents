using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowStartTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowStartTriggers",
                columns: table => new
                {
                    TriggerKey = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DefinitionKey = table.Column<int>(type: "integer", nullable: false),
                    DefinitionId = table.Column<string>(type: "text", nullable: true),
                    DefinitionVersion = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    NodeId = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    SignalName = table.Column<string>(type: "text", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    AdoptCorrelationKey = table.Column<bool>(type: "boolean", nullable: false),
                    Pattern = table.Column<string>(type: "text", nullable: true),
                    VariablesJson = table.Column<string>(type: "text", nullable: true),
                    SkipWhilePreviousRuns = table.Column<bool>(type: "boolean", nullable: false),
                    NextDueUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRunUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastInstanceId = table.Column<string>(type: "text", nullable: true),
                    LeaseOwner = table.Column<string>(type: "text", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStartTriggers", x => x.TriggerKey);
                    table.ForeignKey(
                        name: "FK_WorkflowStartTriggers_WorkflowDefinitions_DefinitionKey",
                        column: x => x.DefinitionKey,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "DefinitionKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStartTriggers_DefinitionKey",
                table: "WorkflowStartTriggers",
                column: "DefinitionKey");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStartTriggers_Kind_NextDueUtc",
                table: "WorkflowStartTriggers",
                columns: new[] { "Kind", "NextDueUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStartTriggers_Kind_SignalName",
                table: "WorkflowStartTriggers",
                columns: new[] { "Kind", "SignalName" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStartTriggers_TenantId_DefinitionId",
                table: "WorkflowStartTriggers",
                columns: new[] { "TenantId", "DefinitionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowStartTriggers");
        }
    }
}
