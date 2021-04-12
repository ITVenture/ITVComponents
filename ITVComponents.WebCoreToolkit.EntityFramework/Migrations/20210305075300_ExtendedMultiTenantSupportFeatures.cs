using Microsoft.EntityFrameworkCore.Migrations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Migrations
{
    public partial class ExtendedMultiTenantSupportFeatures : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Roles_RoleId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Users_UserId",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_UniquePluginConst",
                table: "WebPluginConstants");

            migrationBuilder.DropIndex(
                name: "IX_UniqueRoleName",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_UniquePermissionName",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "IsGlobal",
                table: "Permissions");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "UserRoles",
                newName: "TenantUserId");

            migrationBuilder.RenameIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles",
                newName: "IX_UserRoles_TenantUserId");

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "WebPlugins",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "WebPluginConstants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PermissionId",
                table: "TenantNavigation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemRole",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Roles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Permissions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PluginNameUniqueness",
                table: "WebPlugins",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                computedColumnSql: "case when TenantId is null then UniqueName else '__T'+convert(varchar(10),TenantId)+'##'+UniqueName end persisted");

            migrationBuilder.AddColumn<string>(
                name: "NameUniqueness",
                table: "WebPluginConstants",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                computedColumnSql: "case when TenantId is null then Name else '__T'+convert(varchar(10),TenantId)+'##'+Name end persisted");

            migrationBuilder.AddColumn<string>(
                name: "RoleNameUniqueness",
                table: "Roles",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                computedColumnSql: "'__T'+convert(varchar(10),TenantId)+'##'+RoleName persisted");

            migrationBuilder.AddColumn<string>(
                name: "PermissionNameUniqueness",
                table: "Permissions",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                computedColumnSql: "case when TenantId is null then PermissionName else '__T'+convert(varchar(10),TenantId)+'##'+PermissionName end persisted");

            migrationBuilder.CreateTable(
                name: "TenantUsers",
                columns: table => new
                {
                    TenantUserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUsers", x => x.TenantUserId);
                    table.ForeignKey(
                        name: "FK_TenantUsers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UniquePluginName",
                table: "WebPlugins",
                column: "PluginNameUniqueness",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebPlugins_TenantId",
                table: "WebPlugins",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UniquePluginConst",
                table: "WebPluginConstants",
                column: "NameUniqueness",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebPluginConstants_TenantId",
                table: "WebPluginConstants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantNavigation_PermissionId",
                table: "TenantNavigation",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId",
                table: "Roles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueRoleName",
                table: "Roles",
                column: "RoleNameUniqueness",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_TenantId",
                table: "Permissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UniquePermissionName",
                table: "Permissions",
                column: "PermissionNameUniqueness",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_TenantId",
                table: "TenantUsers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_UserId",
                table: "TenantUsers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Permissions_Tenants_TenantId",
                table: "Permissions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_Roles_RoleId",
                table: "RolePermissions",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "RoleId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Tenants_TenantId",
                table: "Roles",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenantNavigation_Permissions_PermissionId",
                table: "TenantNavigation",
                column: "PermissionId",
                principalTable: "Permissions",
                principalColumn: "PermissionId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                table: "UserRoles",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "RoleId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_TenantUsers_TenantUserId",
                table: "UserRoles",
                column: "TenantUserId",
                principalTable: "TenantUsers",
                principalColumn: "TenantUserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WebPluginConstants_Tenants_TenantId",
                table: "WebPluginConstants",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WebPlugins_Tenants_TenantId",
                table: "WebPlugins",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Permissions_Tenants_TenantId",
                table: "Permissions");

            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Roles_RoleId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Tenants_TenantId",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantNavigation_Permissions_PermissionId",
                table: "TenantNavigation");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_TenantUsers_TenantUserId",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_WebPluginConstants_Tenants_TenantId",
                table: "WebPluginConstants");

            migrationBuilder.DropForeignKey(
                name: "FK_WebPlugins_Tenants_TenantId",
                table: "WebPlugins");

            migrationBuilder.DropTable(
                name: "TenantUsers");

            migrationBuilder.DropIndex(
                name: "IX_UniquePluginName",
                table: "WebPlugins");

            migrationBuilder.DropIndex(
                name: "IX_WebPlugins_TenantId",
                table: "WebPlugins");

            migrationBuilder.DropIndex(
                name: "IX_UniquePluginConst",
                table: "WebPluginConstants");

            migrationBuilder.DropIndex(
                name: "IX_WebPluginConstants_TenantId",
                table: "WebPluginConstants");

            migrationBuilder.DropIndex(
                name: "IX_TenantNavigation_PermissionId",
                table: "TenantNavigation");

            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_UniqueRoleName",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_TenantId",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_UniquePermissionName",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "PluginNameUniqueness",
                table: "WebPlugins");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "WebPlugins");

            migrationBuilder.DropColumn(
                name: "NameUniqueness",
                table: "WebPluginConstants");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "WebPluginConstants");

            migrationBuilder.DropColumn(
                name: "PermissionId",
                table: "TenantNavigation");

            migrationBuilder.DropColumn(
                name: "IsSystemRole",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "RoleNameUniqueness",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "PermissionNameUniqueness",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Permissions");

            migrationBuilder.RenameColumn(
                name: "TenantUserId",
                table: "UserRoles",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_UserRoles_TenantUserId",
                table: "UserRoles",
                newName: "IX_UserRoles_UserId");

            migrationBuilder.AddColumn<bool>(
                name: "IsGlobal",
                table: "Permissions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_UniquePluginConst",
                table: "WebPluginConstants",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UniqueRoleName",
                table: "Roles",
                column: "RoleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UniquePermissionName",
                table: "Permissions",
                column: "PermissionName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_Roles_RoleId",
                table: "RolePermissions",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "RoleId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                table: "UserRoles",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "RoleId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Users_UserId",
                table: "UserRoles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
