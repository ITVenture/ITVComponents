using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.SqlServer.Migrations
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
            migrationBuilder.Sql("DELETE FROM BranchLocks");
            migrationBuilder.Sql("DELETE FROM HistoryEntries");
            migrationBuilder.Sql("DELETE FROM Tokens");
            migrationBuilder.Sql("DELETE FROM WorkflowInstances");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowDefinitions",
                table: "WorkflowDefinitions");

            migrationBuilder.AddColumn<int>(
                name: "DefinitionKey",
                table: "WorkflowInstances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefinitionKey",
                table: "WorkflowDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowDefinitions",
                table: "WorkflowDefinitions",
                column: "DefinitionKey");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_DefinitionKey",
                table: "WorkflowInstances",
                column: "DefinitionKey");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_TenantId_Id_Version",
                table: "WorkflowDefinitions",
                columns: new[] { "TenantId", "Id", "Version" },
                unique: true);

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
