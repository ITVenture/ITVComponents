using Microsoft.EntityFrameworkCore.Migrations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Migrations
{
    public partial class TrustedAccessComponents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantPassword",
                table: "Tenants",
                type: "nvarchar(125)",
                maxLength: 125,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TrustedFullAccessComponents",
                columns: table => new
                {
                    TrustedFullAccessComponentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullQualifiedTypeName = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TrustedForGlobals = table.Column<bool>(type: "bit", nullable: false),
                    TrustedForAllTenants = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustedFullAccessComponents", x => x.TrustedFullAccessComponentId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrustedFullAccessComponents");

            migrationBuilder.DropColumn(
                name: "TenantPassword",
                table: "Tenants");
        }
    }
}
