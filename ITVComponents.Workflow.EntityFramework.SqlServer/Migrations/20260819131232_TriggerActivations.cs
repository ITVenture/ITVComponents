using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class TriggerActivations : Migration
    {
        /// <summary>
        /// <b>Von Hand nachbearbeitet</b> - was das Gerüst erzeugt hat, war an zwei Stellen falsch, und
        /// beide hätten Daten still zerstört:
        /// <list type="number">
        ///   <item>
        ///   Es deutete <c>LeaseOwner</c> → <c>RequiredPermission</c> und <c>LastInstanceId</c> →
        ///   <c>RequiredFeature</c> als <b>Umbenennung</b> (gleicher Typ, gleiche Tabelle - für das
        ///   Gerüst nicht unterscheidbar). Damit stünden Runner-Kennungen als Berechtigungsnamen und
        ///   alte Instanz-Ids als Feature-Namen in der Tabelle. Ersetzt durch Anlegen + Fallenlassen.
        ///   </item>
        ///   <item>
        ///   Der <b>Umzug des Lauf-Zustands</b> fehlte ganz: das Gerüst kennt nur den Unterschied
        ///   zweier Schemata, nicht die Absicht dahinter. Ein blosses <c>database update</c> hätte
        ///   jeden Zeitplan-Stand verworfen - und weil dann jeder Plan als „noch nie gelaufen" gälte,
        ///   liefe jedes Muster mit „sofort"-Kennzeichen beim ersten Poll los.
        ///   </item>
        /// </list>
        /// Deshalb auch die Reihenfolge: erst die Tabelle, dann der Umzug, erst danach die alten Spalten.
        /// </summary>
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowLocalActivation",
                table: "WorkflowStartTriggers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowOwnVariables",
                table: "WorkflowStartTriggers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowReschedule",
                table: "WorkflowStartTriggers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowTenantlessStart",
                table: "WorkflowStartTriggers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "WorkflowStartTriggers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Die beiden, die das Geruest als Umbenennung missverstanden hat.
            migrationBuilder.AddColumn<string>(
                name: "RequiredFeature",
                table: "WorkflowStartTriggers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredPermission",
                table: "WorkflowStartTriggers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkflowStartTriggerActivations",
                columns: table => new
                {
                    ActivationKey = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerTenantId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DefinitionId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    NodeId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    PatternOverride = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VariablesJsonOverride = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NextDueUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastInstanceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeaseOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActivatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStartTriggerActivations", x => x.ActivationKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStartTriggerActivations_Enabled_NextDueUtc",
                table: "WorkflowStartTriggerActivations",
                columns: new[] { "Enabled", "NextDueUtc" });

            // Ohne Filter - bewusst. SQL Server haengt an einen eindeutigen Index ueber nullable Spalten
            // von selbst ein "WHERE ... IS NOT NULL" an und naehme damit ausgerechnet die OEFFENTLICHEN
            // Ausloeser von der Pruefung aus, also genau den Fall, um den es geht. Ohne Filter zaehlen
            // NULLs bei SQL Server als gleich: hoechstens eine Uebernahme je Ausloeser und Mandant.
            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStartTriggerActivations_OwnerTenantId_DefinitionId_NodeId_Kind_TenantId",
                table: "WorkflowStartTriggerActivations",
                columns: new[] { "OwnerTenantId", "DefinitionId", "NodeId", "Kind", "TenantId" },
                unique: true);

            // DER Umzug. Je bestehendem Ausloeser genau EINE Aktivierung - sein eigener Mandant ist
            // zugleich Besitzer und Betreiber, denn oeffentliche Definitionen hatten bis hierher gar
            // keine Ausloeser. LastRunUtc MUSS mit: ohne ihn gilt jeder Plan als "noch nie gelaufen",
            // und jedes Muster mit "sofort"-Kennzeichen laeuft beim ersten Poll los.
            migrationBuilder.Sql(@"
INSERT INTO [WorkflowStartTriggerActivations]
    ([OwnerTenantId], [DefinitionId], [NodeId], [Kind], [TenantId], [Enabled],
     [NextDueUtc], [LastRunUtc], [LastInstanceId], [LeaseOwner], [LeaseUntilUtc],
     [ActivatedBy], [ActivatedUtc])
SELECT t.[TenantId], t.[DefinitionId], t.[NodeId], t.[Kind], t.[TenantId], 1,
       t.[NextDueUtc], t.[LastRunUtc], t.[LastInstanceId], t.[LeaseOwner], t.[LeaseUntilUtc],
       N'(migriert)', SYSUTCDATETIME()
FROM [WorkflowStartTriggers] t;");

            // IsPublic nachziehen. Sollte nichts treffen (oeffentliche Definitionen bekamen bisher keine
            // Ausloeser) - steht hier, damit man sich darauf nicht verlassen muss.
            migrationBuilder.Sql(@"
UPDATE t SET t.[IsPublic] = 1
FROM [WorkflowStartTriggers] t
INNER JOIN [WorkflowDefinitions] d ON d.[DefinitionKey] = t.[DefinitionKey]
WHERE d.[TenantId] IS NULL;");

            // ERST JETZT die alten Spalten. Vorher waeren sie beim Umzug oben schon weg gewesen.
            migrationBuilder.DropIndex(
                name: "IX_WorkflowStartTriggers_Kind_NextDueUtc",
                table: "WorkflowStartTriggers");

            migrationBuilder.DropColumn(
                name: "NextDueUtc",
                table: "WorkflowStartTriggers");

            migrationBuilder.DropColumn(
                name: "LastRunUtc",
                table: "WorkflowStartTriggers");

            migrationBuilder.DropColumn(
                name: "LastInstanceId",
                table: "WorkflowStartTriggers");

            migrationBuilder.DropColumn(
                name: "LeaseOwner",
                table: "WorkflowStartTriggers");

            migrationBuilder.DropColumn(
                name: "LeaseUntilUtc",
                table: "WorkflowStartTriggers");
        }

        /// <inheritdoc />
        /// <summary>
        /// Der Rueckweg holt den Lauf-Zustand aus den Aktivierungen zurueck, bevor die Tabelle faellt -
        /// sonst waere ein Zurueckrollen genau der Datenverlust, den <c>Up</c> vermeidet. Zurueck kann
        /// nur EIN Stand je Ausloeser: haben mehrere Mandanten denselben zentralen Plan uebernommen,
        /// ueberlebt der des Besitzers. Mehr gibt das alte Schema nicht her.
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextDueUtc",
                table: "WorkflowStartTriggers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRunUtc",
                table: "WorkflowStartTriggers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastInstanceId",
                table: "WorkflowStartTriggers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseOwner",
                table: "WorkflowStartTriggers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseUntilUtc",
                table: "WorkflowStartTriggers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE t
SET t.[NextDueUtc]     = a.[NextDueUtc],
    t.[LastRunUtc]     = a.[LastRunUtc],
    t.[LastInstanceId] = a.[LastInstanceId],
    t.[LeaseOwner]     = a.[LeaseOwner],
    t.[LeaseUntilUtc]  = a.[LeaseUntilUtc]
FROM [WorkflowStartTriggers] t
INNER JOIN [WorkflowStartTriggerActivations] a
        ON a.[DefinitionId] = t.[DefinitionId]
       AND a.[NodeId]       = t.[NodeId]
       AND a.[Kind]         = t.[Kind]
       AND ((a.[OwnerTenantId] IS NULL AND t.[TenantId] IS NULL)
            OR a.[OwnerTenantId] = t.[TenantId])
       AND ((a.[TenantId] IS NULL AND t.[TenantId] IS NULL)
            OR a.[TenantId] = t.[TenantId]);");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStartTriggers_Kind_NextDueUtc",
                table: "WorkflowStartTriggers",
                columns: new[] { "Kind", "NextDueUtc" });

            migrationBuilder.DropTable(
                name: "WorkflowStartTriggerActivations");

            migrationBuilder.DropColumn(
                name: "AllowLocalActivation",
                table: "WorkflowStartTriggers");

            migrationBuilder.DropColumn(
                name: "AllowOwnVariables",
                table: "WorkflowStartTriggers");

            migrationBuilder.DropColumn(
                name: "AllowReschedule",
                table: "WorkflowStartTriggers");

            migrationBuilder.DropColumn(
                name: "AllowTenantlessStart",
                table: "WorkflowStartTriggers");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "WorkflowStartTriggers");

            migrationBuilder.DropColumn(
                name: "RequiredFeature",
                table: "WorkflowStartTriggers");

            migrationBuilder.DropColumn(
                name: "RequiredPermission",
                table: "WorkflowStartTriggers");
        }
    }
}
