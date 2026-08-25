using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.Workflow.EntityFramework.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class RetentionOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowRetentionOverrides",
                columns: table => new
                {
                    RetentionOverrideKey = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerTenantId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DefinitionId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RetentionDays = table.Column<int>(type: "int", nullable: true),
                    AttachmentRetentionDays = table.Column<int>(type: "int", nullable: true),
                    SetBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SetUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRetentionOverrides", x => x.RetentionOverrideKey);
                });

            // Ohne Filter - bewusst. SQL Server haengt an einen eindeutigen Index ueber nullable
            // Spalten von selbst ein "WHERE ... IS NOT NULL" an und naehme damit ausgerechnet die
            // oeffentlichen Definitionen und den mandantenfreien Betrieb von der Pruefung aus, also den
            // Regelfall. Ohne Filter zaehlen NULLs bei SQL Server als gleich: hoechstens ein Widerspruch
            // je Definition und Mandant. Genau das ist gemeint (das Gegenstueck steht im Modell als
            // HasFilter(null); in der PostgreSQL-Migration heisst es NULLS NOT DISTINCT).
            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRetentionOverrides_OwnerTenantId_DefinitionId_TenantId",
                table: "WorkflowRetentionOverrides",
                columns: new[] { "OwnerTenantId", "DefinitionId", "TenantId" },
                unique: true);

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
