using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Migrations
{
    public partial class WidgetLocals : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TitleTemplate",
                table: "Widgets",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Widgets",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "WidgetLocales",
                columns: table => new
                {
                    DashboardWidgetLocalizationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DashboardWidgetId = table.Column<int>(type: "int", nullable: false),
                    LocaleName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    TitleTemplate = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Template = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WidgetLocales", x => x.DashboardWidgetLocalizationId);
                    table.ForeignKey(
                        name: "FK_WidgetLocales_Widgets_DashboardWidgetId",
                        column: x => x.DashboardWidgetId,
                        principalTable: "Widgets",
                        principalColumn: "DashboardWidgetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UniqueDashboardLocaleDef",
                table: "WidgetLocales",
                columns: new[] { "DashboardWidgetId", "LocaleName" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WidgetLocales");

            migrationBuilder.AlterColumn<string>(
                name: "TitleTemplate",
                table: "Widgets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Widgets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024,
                oldNullable: true);
        }
    }
}
