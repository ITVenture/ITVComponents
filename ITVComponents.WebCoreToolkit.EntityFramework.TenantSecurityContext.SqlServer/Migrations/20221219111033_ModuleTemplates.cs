using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Migrations
{
    public partial class ModuleTemplates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TemplateModules",
                columns: table => new
                {
                    TemplateModuleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateModuleName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FeatureId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateModules", x => x.TemplateModuleId);
                    table.ForeignKey(
                        name: "FK_TemplateModules_Features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "Features",
                        principalColumn: "FeatureId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateModuleConfigurators",
                columns: table => new
                {
                    TemplateModuleConfiguratorId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomConfiguratorView = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ConfiguratorTypeBack = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    TemplateModuleId = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateModuleConfigurators", x => x.TemplateModuleConfiguratorId);
                    table.ForeignKey(
                        name: "FK_TemplateModuleConfigurators_TemplateModules_TemplateModuleId",
                        column: x => x.TemplateModuleId,
                        principalTable: "TemplateModules",
                        principalColumn: "TemplateModuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateModuleScripts",
                columns: table => new
                {
                    TemplateModuleScriptId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScriptFile = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    TemplateModuleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateModuleScripts", x => x.TemplateModuleScriptId);
                    table.ForeignKey(
                        name: "FK_TemplateModuleScripts_TemplateModules_TemplateModuleId",
                        column: x => x.TemplateModuleId,
                        principalTable: "TemplateModules",
                        principalColumn: "TemplateModuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateModuleConfiguratorParameters",
                columns: table => new
                {
                    TemplateModuleCfgParameterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParameterName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParameterValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TemplateModuleConfiguratorId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateModuleConfiguratorParameters", x => x.TemplateModuleCfgParameterId);
                    table.ForeignKey(
                        name: "FK_TemplateModuleConfiguratorParameters_TemplateModuleConfigurators_TemplateModuleConfiguratorId",
                        column: x => x.TemplateModuleConfiguratorId,
                        principalTable: "TemplateModuleConfigurators",
                        principalColumn: "TemplateModuleConfiguratorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateModuleConfiguratorParameters_TemplateModuleConfiguratorId",
                table: "TemplateModuleConfiguratorParameters",
                column: "TemplateModuleConfiguratorId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateModuleConfigurators_TemplateModuleId",
                table: "TemplateModuleConfigurators",
                column: "TemplateModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateModules_FeatureId",
                table: "TemplateModules",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateModuleScripts_TemplateModuleId",
                table: "TemplateModuleScripts",
                column: "TemplateModuleId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemplateModuleConfiguratorParameters");

            migrationBuilder.DropTable(
                name: "TemplateModuleScripts");

            migrationBuilder.DropTable(
                name: "TemplateModuleConfigurators");

            migrationBuilder.DropTable(
                name: "TemplateModules");
        }
    }
}
