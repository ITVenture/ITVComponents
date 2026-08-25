using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InstanceEndedAndArchive : Migration
    {
        /// <summary>
        /// <b>Von Hand nachbearbeitet</b> - das Geruest kennt nur den Unterschied zweier Schemata, nicht
        /// die Absicht dahinter. Es fehlt der <b>Nachtrag fuer den Altbestand</b>: ohne ihn traegt jeder
        /// bereits beendete Vorgang <c>EndedUtc IS NULL</c>, faellt damit fuer immer aus dem
        /// Aufbewahrungslauf heraus - und die ganze Aufbewahrung gaelte stillschweigend nur fuer das,
        /// was ab jetzt endet. Ausgerechnet die Altlast bliebe liegen.
        /// <para>
        /// Genommen wird <c>UpdatedUtc</c>. Das ist die beste verfuegbare Naeherung und nicht dasselbe:
        /// bei einem Vorgang, den seit seinem Ende jemand angehalten oder fortgesetzt hat, steht dort ein
        /// spaeterer Zeitpunkt, und seine Frist beginnt entsprechend spaeter. Zu spaet aufzuraeumen ist
        /// die richtige Richtung des Fehlers - der umgekehrte Weg raeumte Daten weg, die noch zu halten
        /// waren.
        /// </para>
        /// <para>
        /// Der <c>Down</c>-Weg laesst die Spalte fallen und damit die Endzeitpunkte. Ein erneutes
        /// <c>Up</c> stellt sie aus <c>UpdatedUtc</c> wieder her - naeherungsweise, wie beim ersten Mal.
        /// </para>
        /// </summary>
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndedUtc",
                table: "WorkflowInstances",
                type: "datetime2",
                nullable: true);

            // DER Nachtrag. Status 2/3/4 = Completed/Faulted/Cancelled - die Endstatus.
            migrationBuilder.Sql(@"
UPDATE [WorkflowInstances]
   SET [EndedUtc] = [UpdatedUtc]
 WHERE [EndedUtc] IS NULL AND [Status] IN (2, 3, 4);");

            migrationBuilder.CreateTable(
                name: "WorkflowArchivedInstances",
                columns: table => new
                {
                    InstanceId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DefinitionKey = table.Column<int>(type: "int", nullable: false),
                    DefinitionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefinitionVersion = table.Column<int>(type: "int", nullable: false),
                    DefinitionName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FaultCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FaultMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RootInstanceId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ParentInstanceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ArchivedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentsPurgedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
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
