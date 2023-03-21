using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.PostgreSql.Migrations
{
    public partial class WidgetLocals : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TitleTemplate",
                table: "Widgets",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Widgets",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PluginNameUniqueness",
                table: "WebPlugins",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                computedColumnSql: "case when \"TenantId\" is null then \"UniqueName\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"UniqueName\" end",
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024);

            migrationBuilder.AlterColumn<string>(
                name: "NameUniqueness",
                table: "WebPluginConstants",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                computedColumnSql: "case when \"TenantId\" is null then \"Name\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"Name\" end",
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024);

            migrationBuilder.AlterColumn<string>(
                name: "RoleNameUniqueness",
                table: "Roles",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                computedColumnSql: "'__T'||cast(\"TenantId\" as character varying(10))||'##'||\"RoleName\"",
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024);

            migrationBuilder.AlterColumn<string>(
                name: "PermissionNameUniqueness",
                table: "Permissions",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                computedColumnSql: "case when \"TenantId\" is null then \"PermissionName\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"PermissionName\" end",
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024);

            migrationBuilder.AlterColumn<string>(
                name: "UrlUniqueness",
                table: "Navigation",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                computedColumnSql: "case when COALESCE(\"Url\",'')='' and COALESCE(\"RefTag\",'')='' then 'MENU__'||cast(\"NavigationMenuId\" as character varying(10)) when COALESCE(\"Url\",'')='' then \"RefTag\" else \"Url\" end",
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024);

            migrationBuilder.CreateTable(
                name: "WidgetLocales",
                columns: table => new
                {
                    DashboardWidgetLocalizationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DashboardWidgetId = table.Column<int>(type: "integer", nullable: false),
                    LocaleName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    TitleTemplate = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Template = table.Column<string>(type: "text", nullable: false)
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
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Widgets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PluginNameUniqueness",
                table: "WebPlugins",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldComputedColumnSql: "case when \"TenantId\" is null then \"UniqueName\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"UniqueName\" end");

            migrationBuilder.AlterColumn<string>(
                name: "NameUniqueness",
                table: "WebPluginConstants",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldComputedColumnSql: "case when \"TenantId\" is null then \"Name\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"Name\" end");

            migrationBuilder.AlterColumn<string>(
                name: "RoleNameUniqueness",
                table: "Roles",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldComputedColumnSql: "'__T'||cast(\"TenantId\" as character varying(10))||'##'||\"RoleName\"");

            migrationBuilder.AlterColumn<string>(
                name: "PermissionNameUniqueness",
                table: "Permissions",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldComputedColumnSql: "case when \"TenantId\" is null then \"PermissionName\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"PermissionName\" end");

            migrationBuilder.AlterColumn<string>(
                name: "UrlUniqueness",
                table: "Navigation",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldComputedColumnSql: "case when COALESCE(\"Url\",'')='' and COALESCE(\"RefTag\",'')='' then 'MENU__'||cast(\"NavigationMenuId\" as character varying(10)) when COALESCE(\"Url\",'')='' then \"RefTag\" else \"Url\" end");
        }
    }
}
