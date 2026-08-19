using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.SqlServer.Migrations
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
                    TriggerKey = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DefinitionKey = table.Column<int>(type: "int", nullable: false),
                    DefinitionId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DefinitionVersion = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    NodeId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    SignalName = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    AdoptCorrelationKey = table.Column<bool>(type: "bit", nullable: false),
                    Pattern = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VariablesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SkipWhilePreviousRuns = table.Column<bool>(type: "bit", nullable: false),
                    NextDueUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastInstanceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeaseOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
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
