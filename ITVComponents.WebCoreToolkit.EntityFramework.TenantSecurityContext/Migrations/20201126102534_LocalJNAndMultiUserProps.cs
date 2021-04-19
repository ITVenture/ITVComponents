using Microsoft.EntityFrameworkCore.Migrations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Migrations
{
    public partial class LocalJNAndMultiUserProps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UniqueProperty",
                table: "UserProperties");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Navigation",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_UserProperties_UserId",
                table: "UserProperties",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProperties_UserId",
                table: "UserProperties");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Navigation",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueProperty",
                table: "UserProperties",
                columns: new[] { "UserId", "PropertyName" },
                unique: true);
        }
    }
}
