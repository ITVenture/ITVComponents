using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.PostgreSql.Migrations
{
    public partial class PluginsResize2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
