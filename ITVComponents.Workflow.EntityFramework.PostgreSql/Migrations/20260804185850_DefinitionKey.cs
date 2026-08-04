using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class DefinitionKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Die Verweis-Spalte ist ein echter Fremdschluessel und darf nicht null sein - fuer
            // bestehende Zeilen gaebe es keinen Wert, den man erfinden koennte. Der Bestand wird
            // deshalb VERWORFEN, nicht nachgezogen: eine laufende Instanz ohne eindeutig bestimmbare
            // Definition ist nicht wiederherstellbar. Die Definitionen selbst bleiben.
            //
            // In der Reihenfolge der Abhaengigkeiten, damit es auch ohne Kaskaden durchlaeuft.
            migrationBuilder.Sql("DELETE FROM \"BranchLocks\"");
            migrationBuilder.Sql("DELETE FROM \"HistoryEntries\"");
            migrationBuilder.Sql("DELETE FROM \"Tokens\"");
            migrationBuilder.Sql("DELETE FROM \"WorkflowInstances\"");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowDefinitions",
                table: "WorkflowDefinitions");

            migrationBuilder.AddColumn<int>(
                name: "DefinitionKey",
                table: "WorkflowInstances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefinitionKey",
                table: "WorkflowDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowDefinitions",
                table: "WorkflowDefinitions",
                column: "DefinitionKey");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_DefinitionKey",
                table: "WorkflowInstances",
                column: "DefinitionKey");

            // PostgreSQL behandelt NULLs in einem eindeutigen Index als VERSCHIEDEN - zwei
            // oeffentliche Definitionen (TenantId null) mit gleicher Id und Version kaemen dort also
            // beide durch, waehrend SQL Server sie zu Recht abweist. Der Schutz waere auf einer der
            // beiden Datenbanken stillschweigend wirkungslos.
            //
            // Deshalb ein Ausdrucks-Index ueber COALESCE statt "NULLS NOT DISTINCT": letzteres gibt es
            // erst ab PostgreSQL 15, und ein Migrationsskript, das je nach Server-Version scheitert,
            // waere der schlechtere Handel. Der leere String kann kein Mandantenname sein.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_WorkflowDefinitions_TenantId_Id_Version\" " +
                "ON \"WorkflowDefinitions\" (COALESCE(\"TenantId\", ''), \"Id\", \"Version\")");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowInstances_WorkflowDefinitions_DefinitionKey",
                table: "WorkflowInstances",
                column: "DefinitionKey",
                principalTable: "WorkflowDefinitions",
                principalColumn: "DefinitionKey",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowInstances_WorkflowDefinitions_DefinitionKey",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_DefinitionKey",
                table: "WorkflowInstances");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowDefinitions",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_TenantId_Id_Version",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "DefinitionKey",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "DefinitionKey",
                table: "WorkflowDefinitions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowDefinitions",
                table: "WorkflowDefinitions",
                columns: new[] { "Id", "Version" });
        }
    }
}
