using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Migrations
{
    public partial class ExtendedUserProperties : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProperties_UserId",
                table: "UserProperties");

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "UserProperties",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024);

            migrationBuilder.AddColumn<int>(
                name: "PropertyType",
                table: "UserProperties",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "UniqueUserProp",
                table: "UserProperties",
                columns: new[] { "UserId", "PropertyType", "PropertyName" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UniqueUserProp",
                table: "UserProperties");

            migrationBuilder.DropColumn(
                name: "PropertyType",
                table: "UserProperties");

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "UserProperties",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_UserProperties_UserId",
                table: "UserProperties",
                column: "UserId");
        }
    }
}
