using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.PostgreSql.Migrations
{
    /// <summary>
    /// Zieht den denormalisierten Mandanten an den Token-Zeilen nach, die ihn noch nicht tragen.
    /// Gegenstueck zur gleichnamigen Migration der SQL-Server-Fassung; die Begruendung steht dort.
    /// </summary>
    /// <remarks>
    /// Kein Schema-Change. Der Unterschied zur T-SQL-Fassung ist reine Syntax: PostgreSQL schreibt die
    /// Join-Quelle in ein <c>FROM</c> hinter das <c>SET</c> und kennt kein <c>UPDATE &lt;alias&gt;</c>.
    /// Bezeichner in Anfuehrungszeichen, sonst faltet PostgreSQL sie klein und findet die von EF
    /// angelegten Tabellen nicht.
    /// </remarks>
    public partial class TokenTenantBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                 UPDATE "Tokens" t
                                 SET "TenantId" = i."TenantId"
                                 FROM "WorkflowInstances" i
                                 WHERE i."Id" = t."InstanceId"
                                   AND t."TenantId" IS NULL
                                   AND i."TenantId" IS NOT NULL
                                 """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bewusst leer - siehe SQL-Server-Fassung.
        }
    }
}
