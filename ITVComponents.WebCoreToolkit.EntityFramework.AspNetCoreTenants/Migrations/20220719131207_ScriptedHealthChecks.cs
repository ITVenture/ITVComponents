using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Migrations
{
    public partial class ScriptedHealthChecks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HealthScripts",
                columns: table => new
                {
                    HealthScriptId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HealthScriptName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Script = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthScripts", x => x.HealthScriptId);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_NamedHealthScript",
                table: "HealthScripts",
                column: "HealthScriptName",
                unique: true,
                filter: "[HealthScriptName] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HealthScripts");
        }
    }
}
