using Microsoft.EntityFrameworkCore.Migrations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Migrations
{
    public partial class TTemplatesAndUqTenantNav : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantNavigation_TenantId",
                table: "TenantNavigation");

            migrationBuilder.CreateTable(
                name: "TenantTemplates",
                columns: table => new
                {
                    TenantTemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Markup = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantTemplates", x => x.TenantTemplateId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UniqueTenantMenu",
                table: "TenantNavigation",
                columns: new[] { "TenantId", "NavigationMenuId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureUniqueness",
                table: "Features",
                column: "FeatureName",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantTemplates");

            migrationBuilder.DropIndex(
                name: "IX_UniqueTenantMenu",
                table: "TenantNavigation");

            migrationBuilder.DropIndex(
                name: "IX_FeatureUniqueness",
                table: "Features");

            migrationBuilder.CreateIndex(
                name: "IX_TenantNavigation_TenantId",
                table: "TenantNavigation",
                column: "TenantId");
        }
    }
}
