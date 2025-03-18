using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class newTenantSecurityFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_TenantUsers_TenantUserId",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_UniqueRolePermission",
                table: "RolePermissions");

            migrationBuilder.AlterColumn<int>(
                name: "TenantUserId",
                table: "UserRoles",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "RoleId",
                table: "UserRoles",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<bool>(
                name: "TenantDirty",
                table: "Tenants",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantTypeId",
                table: "Tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoleMetaData",
                table: "Roles",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RoleId",
                table: "RolePermissions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "OriginId",
                table: "RolePermissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoleRoleId",
                table: "RolePermissions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RoleRoles",
                columns: table => new
                {
                    RoleRoleId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PermissiveRoleId = table.Column<int>(type: "integer", nullable: true),
                    PermittedRoleId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleRoles", x => x.RoleRoleId);
                    table.ForeignKey(
                        name: "FK_RoleRoles_Roles_PermissiveRoleId",
                        column: x => x.PermissiveRoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId");
                    table.ForeignKey(
                        name: "FK_RoleRoles_Roles_PermittedRoleId",
                        column: x => x.PermittedRoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId");
                });

            migrationBuilder.CreateTable(
                name: "TenantTypes",
                columns: table => new
                {
                    TenantTypeId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantTypeName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    TypeMetaData = table.Column<string>(type: "text", nullable: true),
                    TenantTemplateId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantTypes", x => x.TenantTypeId);
                    table.ForeignKey(
                        name: "FK_TenantTypes_TenantTemplates_TenantTemplateId",
                        column: x => x.TenantTemplateId,
                        principalTable: "TenantTemplates",
                        principalColumn: "TenantTemplateId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantTypeId",
                table: "Tenants",
                column: "TenantTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_OriginId",
                table: "RolePermissions",
                column: "OriginId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleRoleId",
                table: "RolePermissions",
                column: "RoleRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueRolePermission",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId", "TenantId", "OriginId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleRoles_PermissiveRoleId",
                table: "RoleRoles",
                column: "PermissiveRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleRoles_PermittedRoleId",
                table: "RoleRoles",
                column: "PermittedRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantTypes_TenantTemplateId",
                table: "TenantTypes",
                column: "TenantTemplateId");

            migrationBuilder.CreateIndex(
                name: "UQ_TenantTypeName",
                table: "TenantTypes",
                column: "TenantTypeName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_RolePermissions_OriginId",
                table: "RolePermissions",
                column: "OriginId",
                principalTable: "RolePermissions",
                principalColumn: "RolePermissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_RoleRoles_RoleRoleId",
                table: "RolePermissions",
                column: "RoleRoleId",
                principalTable: "RoleRoles",
                principalColumn: "RoleRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_TenantTypes_TenantTypeId",
                table: "Tenants",
                column: "TenantTypeId",
                principalTable: "TenantTypes",
                principalColumn: "TenantTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_TenantUsers_TenantUserId",
                table: "UserRoles",
                column: "TenantUserId",
                principalTable: "TenantUsers",
                principalColumn: "TenantUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_RolePermissions_OriginId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_RoleRoles_RoleRoleId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_TenantTypes_TenantTypeId",
                table: "Tenants");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_TenantUsers_TenantUserId",
                table: "UserRoles");

            migrationBuilder.DropTable(
                name: "RoleRoles");

            migrationBuilder.DropTable(
                name: "TenantTypes");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_TenantTypeId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_OriginId",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_RoleRoleId",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_UniqueRolePermission",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "TenantDirty",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TenantTypeId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RoleMetaData",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "OriginId",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "RoleRoleId",
                table: "RolePermissions");

            migrationBuilder.AlterColumn<int>(
                name: "TenantUserId",
                table: "UserRoles",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RoleId",
                table: "UserRoles",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RoleId",
                table: "RolePermissions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UniqueRolePermission",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId", "TenantId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_TenantUsers_TenantUserId",
                table: "UserRoles",
                column: "TenantUserId",
                principalTable: "TenantUsers",
                principalColumn: "TenantUserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
