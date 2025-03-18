using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Migrations
{
    /// <inheritdoc />
    public partial class RolePermissionTypeFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UniqueRolePermission",
                table: "RolePermissions");

            migrationBuilder.AlterColumn<int>(
                name: "RoleId",
                table: "RolePermissions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UniqueRolePermission",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId", "TenantId", "OriginId" },
                unique: true,
                filter: "[OriginId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UniqueRolePermission",
                table: "RolePermissions");

            migrationBuilder.AlterColumn<int>(
                name: "RoleId",
                table: "RolePermissions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueRolePermission",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId", "TenantId", "OriginId" },
                unique: true,
                filter: "[RoleId] IS NOT NULL AND [OriginId] IS NOT NULL");
        }
    }
}
