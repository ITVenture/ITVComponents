using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InstanceEndedAndArchive : Migration
    {
        /// <summary>
        /// <b>Von Hand nachbearbeitet</b> - siehe die gleichnamige Migration im SqlServer-Projekt. Das
        /// Geruest liess den <b>Nachtrag fuer den Altbestand</b> weg; ohne ihn traegt jeder bereits
        /// beendete Vorgang <c>EndedUtc IS NULL</c> und faellt fuer immer aus dem Aufbewahrungslauf
        /// heraus - die Aufbewahrung gaelte dann stillschweigend nur fuer das, was ab jetzt endet.
        /// <para>
        /// Genommen wird <c>UpdatedUtc</c>: die beste verfuegbare Naeherung. Wo seit dem Ende noch
        /// jemand angehalten oder fortgesetzt hat, beginnt die Frist entsprechend spaeter - und zu spaet
        /// aufzuraeumen ist die richtige Richtung des Fehlers.
        /// </para>
        /// </summary>
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndedUtc",
                table: "WorkflowInstances",
                type: "timestamp with time zone",
                nullable: true);

            // DER Nachtrag. Status 2/3/4 = Completed/Faulted/Cancelled - die Endstatus.
            migrationBuilder.Sql(@"
UPDATE ""WorkflowInstances""
   SET ""EndedUtc"" = ""UpdatedUtc""
 WHERE ""EndedUtc"" IS NULL AND ""Status"" IN (2, 3, 4);");

            migrationBuilder.CreateTable(
                name: "WorkflowArchivedInstances",
                columns: table => new
                {
                    InstanceId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    DefinitionKey = table.Column<int>(type: "integer", nullable: false),
                    DefinitionId = table.Column<string>(type: "text", nullable: true),
                    DefinitionVersion = table.Column<int>(type: "integer", nullable: false),
                    DefinitionName = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FaultCode = table.Column<string>(type: "text", nullable: true),
                    FaultMessage = table.Column<string>(type: "text", nullable: true),
                    RootInstanceId = table.Column<string>(type: "text", nullable: true),
                    ParentInstanceId = table.Column<string>(type: "text", nullable: true),
                    ArchivedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    AttachmentsPurgedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowArchivedInstances", x => x.InstanceId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_DefinitionKey_TenantId_EndedUtc",
                table: "WorkflowInstances",
                columns: new[] { "DefinitionKey", "TenantId", "EndedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowArchivedInstances_AttachmentsPurgedUtc_EndedUtc",
                table: "WorkflowArchivedInstances",
                columns: new[] { "AttachmentsPurgedUtc", "EndedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowArchivedInstances_RootInstanceId",
                table: "WorkflowArchivedInstances",
                column: "RootInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowArchivedInstances_TenantId_EndedUtc",
                table: "WorkflowArchivedInstances",
                columns: new[] { "TenantId", "EndedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowArchivedInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_DefinitionKey_TenantId_EndedUtc",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "EndedUtc",
                table: "WorkflowInstances");
        }
    }
}
