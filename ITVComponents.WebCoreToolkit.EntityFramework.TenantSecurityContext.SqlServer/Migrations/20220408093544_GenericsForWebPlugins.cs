using Microsoft.EntityFrameworkCore.Migrations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Migrations
{
    public partial class GenericsForWebPlugins : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefTag",
                table: "Navigation",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UrlUniqueness",
                table: "Navigation",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                computedColumnSql: "case when isnull(Url,'')='' and isnull(RefTag,'')='' then 'MENU__'+convert(varchar(10),NavigationMenuId) when isnull(Url,'')='' then RefTag else Url end persisted",
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024,
                oldComputedColumnSql: "case when isnull(Url,'')='' then 'MENU__'+convert(varchar(10),NavigationMenuId) else Url end persisted");

            migrationBuilder.CreateTable(
                name: "GenericPluginParams",
                columns: table => new
                {
                    WebPluginGenericParameterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WebPluginId = table.Column<int>(type: "int", nullable: false),
                    GenericTypeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TypeExpression = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenericPluginParams", x => x.WebPluginGenericParameterId);
                    table.ForeignKey(
                        name: "FK_GenericPluginParams_WebPlugins_WebPluginId",
                        column: x => x.WebPluginId,
                        principalTable: "WebPlugins",
                        principalColumn: "WebPluginId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UniqueDashboardDef",
                table: "Widgets",
                column: "SystemName",
                unique: true,
                filter: "[SystemName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueGenericParamName",
                table: "GenericPluginParams",
                columns: new[] { "WebPluginId", "GenericTypeName" },
                unique: true,
                filter: "[GenericTypeName] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GenericPluginParams");

            migrationBuilder.DropIndex(
                name: "IX_UniqueDashboardDef",
                table: "Widgets");

            migrationBuilder.DropColumn(
                name: "RefTag",
                table: "Navigation");

            migrationBuilder.AlterColumn<string>(
                name: "UrlUniqueness",
                table: "Navigation",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                computedColumnSql: "case when isnull(Url,'')='' then 'MENU__'+convert(varchar(10),NavigationMenuId) else Url end persisted",
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024,
                oldComputedColumnSql: "case when isnull(Url,'')='' and isnull(RefTag,'')='' then 'MENU__'+convert(varchar(10),NavigationMenuId) when isnull(Url,'')='' then RefTag else Url end persisted");
        }
    }
}
