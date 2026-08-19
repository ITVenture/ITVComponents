using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class TriggerActivations : Migration
    {
        /// <summary>
        /// <b>Von Hand nachbearbeitet</b> - siehe die gleichnamige Migration im SqlServer-Projekt. Das
        /// Geruest deutete zwei Spalten als Umbenennung (<c>LeaseOwner</c> → <c>RequiredPermission</c>,
        /// <c>LastInstanceId</c> → <c>RequiredFeature</c>) und liess den Umzug des Lauf-Zustands ganz
        /// weg; beides haette Daten still zerstoert.
        /// <para>
        /// Hier zusaetzlich: der eindeutige Index braucht <c>NULLS NOT DISTINCT</c>. PostgreSQL
        /// behandelt NULLs im eindeutigen Index standardmaessig als VERSCHIEDEN - ohne den Zusatz waere
        /// der Schutz fuer oeffentliche Ausloeser (Besitzer NULL) und den mandantenfreien Betrieb
        /// stillschweigend wirkungslos. Deshalb steht er als reines SQL da und nicht als CreateIndex.
        /// </para>
        /// </summary>
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowLocalActivation",
                table: "WorkflowStartTriggers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowOwnVariables",
                table: "WorkflowStartTriggers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowReschedule",
                table: "WorkflowStartTriggers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowTenantlessStart",
                table: "WorkflowStartTriggers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "WorkflowStartTriggers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Die beiden, die das Geruest als Umbenennung missverstanden hat.
            migrationBuilder.AddColumn<string>(
                name: "RequiredFeature",
                table: "WorkflowStartTriggers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredPermission",
                table: "WorkflowStartTriggers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkflowStartTriggerActivations",
                columns: table => new
                {
                    ActivationKey = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerTenantId = table.Column<string>(type: "text", nullable: true),
                    DefinitionId = table.Column<string>(type: "text", nullable: true),
                    NodeId = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    PatternOverride = table.Column<string>(type: "text", nullable: true),
                    VariablesJsonOverride = table.Column<string>(type: "text", nullable: true),
                    NextDueUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRunUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastInstanceId = table.Column<string>(type: "text", nullable: true),
                    LeaseOwner = table.Column<string>(type: "text", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActivatedBy = table.Column<string>(type: "text", nullable: true),
                    ActivatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStartTriggerActivations", x => x.ActivationKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStartTriggerActivations_Enabled_NextDueUtc",
                table: "WorkflowStartTriggerActivations",
                columns: new[] { "Enabled", "NextDueUtc" });

            // NICHT ueber CreateIndex: das erzeugte NULLS DISTINCT (die Vorgabe), und damit koennte
            // derselbe oeffentliche Ausloeser von demselben Mandanten beliebig oft uebernommen werden -
            // der eindeutige Index waere ein Index ohne Zusage. Der Name ist der, den das Geruest
            // vergibt (auf 63 Zeichen gekuerzt); er muss so bleiben, sonst sucht eine kuenftige
            // Migration einen Index, den es nicht gibt.
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX ""IX_WorkflowStartTriggerActivations_OwnerTenantId_DefinitionId_~""
    ON ""WorkflowStartTriggerActivations""
    (""OwnerTenantId"", ""DefinitionId"", ""NodeId"", ""Kind"", ""TenantId"") NULLS NOT DISTINCT;");

            // DER Umzug. Je bestehendem Ausloeser genau EINE Aktivierung - sein eigener Mandant ist
            // zugleich Besitzer und Betreiber, denn oeffentliche Definitionen hatten bis hierher gar
            // keine Ausloeser. LastRunUtc MUSS mit: ohne ihn gilt jeder Plan als "noch nie gelaufen",
            // und jedes Muster mit "sofort"-Kennzeichen laeuft beim ersten Poll los.
            migrationBuilder.Sql(@"
INSERT INTO ""WorkflowStartTriggerActivations""
    (""OwnerTenantId"", ""DefinitionId"", ""NodeId"", ""Kind"", ""TenantId"", ""Enabled"",
     ""NextDueUtc"", ""LastRunUtc"", ""LastInstanceId"", ""LeaseOwner"", ""LeaseUntilUtc"",
     ""ActivatedBy"", ""ActivatedUtc"")
SELECT t.""TenantId"", t.""DefinitionId"", t.""NodeId"", t.""Kind"", t.""TenantId"", TRUE,
       t.""NextDueUtc"", t.""LastRunUtc"", t.""LastInstanceId"", t.""LeaseOwner"", t.""LeaseUntilUtc"",
       '(migriert)', NOW() AT TIME ZONE 'UTC'
FROM ""WorkflowStartTriggers"" t;");

            // IsPublic nachziehen. Sollte nichts treffen (oeffentliche Definitionen bekamen bisher keine
            // Ausloeser) - steht hier, damit man sich darauf nicht verlassen muss.
            migrationBuilder.Sql(@"
UPDATE ""WorkflowStartTriggers"" t SET ""IsPublic"" = TRUE
FROM ""WorkflowDefinitions"" d
WHERE d.""DefinitionKey"" = t.""DefinitionKey"" AND d.""TenantId"" IS NULL;");

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
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRunUtc",
                table: "WorkflowStartTriggers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastInstanceId",
                table: "WorkflowStartTriggers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseOwner",
                table: "WorkflowStartTriggers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseUntilUtc",
                table: "WorkflowStartTriggers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE ""WorkflowStartTriggers"" t
SET ""NextDueUtc""     = a.""NextDueUtc"",
    ""LastRunUtc""     = a.""LastRunUtc"",
    ""LastInstanceId"" = a.""LastInstanceId"",
    ""LeaseOwner""     = a.""LeaseOwner"",
    ""LeaseUntilUtc""  = a.""LeaseUntilUtc""
FROM ""WorkflowStartTriggerActivations"" a
WHERE a.""DefinitionId"" = t.""DefinitionId""
  AND a.""NodeId""       = t.""NodeId""
  AND a.""Kind""         = t.""Kind""
  AND a.""OwnerTenantId"" IS NOT DISTINCT FROM t.""TenantId""
  AND a.""TenantId""      IS NOT DISTINCT FROM t.""TenantId"";");

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
