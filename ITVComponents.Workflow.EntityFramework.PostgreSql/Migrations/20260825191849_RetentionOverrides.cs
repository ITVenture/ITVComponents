using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class RetentionOverrides : Migration
    {
        /// <summary>
        /// <b>Von Hand nachbearbeitet</b> - der eindeutige Index braucht <c>NULLS NOT DISTINCT</c>.
        /// PostgreSQL behandelt NULLs im eindeutigen Index standardmaessig als VERSCHIEDEN, und hier
        /// sind <b>alle drei</b> Spalten nullable: die oeffentliche Definition traegt keinen Besitzer,
        /// und im mandantenfreien Betrieb traegt auch der Widersprechende nichts. Ohne den Zusatz waere
        /// der Schutz genau dort wirkungslos, wo er gebraucht wird - und zwar stillschweigend. Dieselbe
        /// Stelle wie bei <c>TriggerActivations</c>.
        /// <para>
        /// Der Indexname ist der, den das Geruest vergibt (auf 63 Zeichen gekuerzt, mit Tilde); er muss
        /// so bleiben, sonst sucht eine kuenftige Migration einen Index, den es nicht gibt.
        /// </para>
        /// </summary>
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowRetentionOverrides",
                columns: table => new
                {
                    RetentionOverrideKey = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerTenantId = table.Column<string>(type: "text", nullable: true),
                    DefinitionId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    RetentionDays = table.Column<int>(type: "integer", nullable: true),
                    AttachmentRetentionDays = table.Column<int>(type: "integer", nullable: true),
                    SetBy = table.Column<string>(type: "text", nullable: true),
                    SetUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRetentionOverrides", x => x.RetentionOverrideKey);
                });

            // NICHT ueber CreateIndex: das erzeugte NULLS DISTINCT (die Vorgabe), und damit koennte
            // derselbe Mandant derselben oeffentlichen Definition beliebig oft widersprechen - der
            // eindeutige Index waere ein Index ohne Zusage.
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX ""IX_WorkflowRetentionOverrides_OwnerTenantId_DefinitionId_Tenan~""
    ON ""WorkflowRetentionOverrides""
    (""OwnerTenantId"", ""DefinitionId"", ""TenantId"") NULLS NOT DISTINCT;");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRetentionOverrides_TenantId",
                table: "WorkflowRetentionOverrides",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowRetentionOverrides");
        }
    }
}
