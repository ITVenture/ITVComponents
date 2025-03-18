using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.PostgreSql.Migrations
{
    public partial class PluginsResize1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UniquePluginName",
                table: "WebPlugins");

            migrationBuilder.AlterColumn<string>(
                name: "PluginNameUniqueness",
                table: "WebPlugins",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldComputedColumnSql: "case when \"TenantId\" is null then \"UniqueName\" else '__T'||cast(\"TenantId\" as character varying(10))||'##'||\"UniqueName\" end");

            migrationBuilder.AlterColumn<string>(
                name: "UniqueName",
                table: "WebPlugins",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "StartupRegistrationConstructor",
                table: "WebPlugins",
                type: "character varying(8192)",
                maxLength: 8192,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Constructor",
                table: "WebPlugins",
                type: "character varying(8192)",
                maxLength: 8192,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048,
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UniqueName",
                table: "WebPlugins",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "StartupRegistrationConstructor",
                table: "WebPlugins",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8192)",
                oldMaxLength: 8192,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Constructor",
                table: "WebPlugins",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8192)",
                oldMaxLength: 8192,
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
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UniquePluginName",
                table: "WebPlugins",
                column: "PluginNameUniqueness",
                unique: true);
        }
    }
}
