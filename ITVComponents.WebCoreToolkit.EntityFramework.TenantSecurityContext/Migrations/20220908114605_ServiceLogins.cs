using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Migrations
{
    public partial class ServiceLogins : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "TenantUsers",
                type: "bit",
                nullable: true,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.CreateTable(
                name: "AppPermissionSets",
                columns: table => new
                {
                    AppPermissionSetId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPermissionSets", x => x.AppPermissionSetId);
                });

            migrationBuilder.CreateTable(
                name: "ClientApps",
                columns: table => new
                {
                    ClientAppId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClientSecret = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientApps", x => x.ClientAppId);
                });

            migrationBuilder.CreateTable(
                name: "ClientAppTemplates",
                columns: table => new
                {
                    ClientAppTemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientAppTemplates", x => x.ClientAppTemplateId);
                });

            migrationBuilder.CreateTable(
                name: "AppPermissions",
                columns: table => new
                {
                    AppPermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppPermissionSetId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPermissions", x => x.AppPermissionId);
                    table.ForeignKey(
                        name: "FK_AppPermissions_AppPermissionSets_AppPermissionSetId",
                        column: x => x.AppPermissionSetId,
                        principalTable: "AppPermissionSets",
                        principalColumn: "AppPermissionSetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientAppPermissions",
                columns: table => new
                {
                    ClientAppPermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientAppId = table.Column<int>(type: "int", nullable: false),
                    AppPermissionSetId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientAppPermissions", x => x.ClientAppPermissionId);
                    table.ForeignKey(
                        name: "FK_ClientAppPermissions_AppPermissionSets_AppPermissionSetId",
                        column: x => x.AppPermissionSetId,
                        principalTable: "AppPermissionSets",
                        principalColumn: "AppPermissionSetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientAppPermissions_ClientApps_ClientAppId",
                        column: x => x.ClientAppId,
                        principalTable: "ClientApps",
                        principalColumn: "ClientAppId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientAppUsers",
                columns: table => new
                {
                    ClientAppUserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TenantUserId = table.Column<int>(type: "int", nullable: false),
                    ClientAppId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientAppUsers", x => x.ClientAppUserId);
                    table.ForeignKey(
                        name: "FK_ClientAppUsers_ClientApps_ClientAppId",
                        column: x => x.ClientAppId,
                        principalTable: "ClientApps",
                        principalColumn: "ClientAppId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientAppUsers_TenantUsers_TenantUserId",
                        column: x => x.TenantUserId,
                        principalTable: "TenantUsers",
                        principalColumn: "TenantUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientAppTemplatePermissions",
                columns: table => new
                {
                    ClientAppTemplatePermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientAppTemplateId = table.Column<int>(type: "int", nullable: false),
                    AppPermissionSetId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientAppTemplatePermissions", x => x.ClientAppTemplatePermissionId);
                    table.ForeignKey(
                        name: "FK_ClientAppTemplatePermissions_AppPermissionSets_AppPermissionSetId",
                        column: x => x.AppPermissionSetId,
                        principalTable: "AppPermissionSets",
                        principalColumn: "AppPermissionSetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientAppTemplatePermissions_ClientAppTemplates_ClientAppTemplateId",
                        column: x => x.ClientAppTemplateId,
                        principalTable: "ClientAppTemplates",
                        principalColumn: "ClientAppTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppPermissions_AppPermissionSetId",
                table: "AppPermissions",
                column: "AppPermissionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPermissions_PermissionId",
                table: "AppPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "UQ_AppPermissionSetName",
                table: "AppPermissionSets",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientAppPermissions_AppPermissionSetId",
                table: "ClientAppPermissions",
                column: "AppPermissionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientAppPermissions_ClientAppId",
                table: "ClientAppPermissions",
                column: "ClientAppId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientAppTemplatePermissions_AppPermissionSetId",
                table: "ClientAppTemplatePermissions",
                column: "AppPermissionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientAppTemplatePermissions_ClientAppTemplateId",
                table: "ClientAppTemplatePermissions",
                column: "ClientAppTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_UniqueTemplateName",
                table: "ClientAppTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientAppUsers_ClientAppId",
                table: "ClientAppUsers",
                column: "ClientAppId");

            migrationBuilder.CreateIndex(
                name: "UQ_ClientAppUser",
                table: "ClientAppUsers",
                column: "Label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_TUserPerApp",
                table: "ClientAppUsers",
                columns: new[] { "TenantUserId", "ClientAppId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppPermissions");

            migrationBuilder.DropTable(
                name: "ClientAppPermissions");

            migrationBuilder.DropTable(
                name: "ClientAppTemplatePermissions");

            migrationBuilder.DropTable(
                name: "ClientAppUsers");

            migrationBuilder.DropTable(
                name: "AppPermissionSets");

            migrationBuilder.DropTable(
                name: "ClientAppTemplates");

            migrationBuilder.DropTable(
                name: "ClientApps");

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "TenantUsers",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true,
                oldDefaultValue: true);
        }
    }
}
