using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Migrations
{
    public partial class FeatureHandling : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_AuthenticationTypes_AuthenticationTypeId",
                table: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "AuthenticationTypeId",
                table: "Users",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "FeatureId",
                table: "Navigation",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Features",
                columns: table => new
                {
                    FeatureId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeatureName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FeatureDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Features", x => x.FeatureId);
                });

            migrationBuilder.CreateTable(
                name: "TenantFeatureActivations",
                columns: table => new
                {
                    TenantFeatureActivationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeatureId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ActivationStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivationEnd = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantFeatureActivations", x => x.TenantFeatureActivationId);
                    table.ForeignKey(
                        name: "FK_TenantFeatureActivations_Features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "Features",
                        principalColumn: "FeatureId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantFeatureActivations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Navigation_FeatureId",
                table: "Navigation",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantFeatureActivations_FeatureId",
                table: "TenantFeatureActivations",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantFeatureActivations_TenantId",
                table: "TenantFeatureActivations",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Navigation_Features_FeatureId",
                table: "Navigation",
                column: "FeatureId",
                principalTable: "Features",
                principalColumn: "FeatureId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_AuthenticationTypes_AuthenticationTypeId",
                table: "Users",
                column: "AuthenticationTypeId",
                principalTable: "AuthenticationTypes",
                principalColumn: "AuthenticationTypeId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Navigation_Features_FeatureId",
                table: "Navigation");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_AuthenticationTypes_AuthenticationTypeId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "TenantFeatureActivations");

            migrationBuilder.DropTable(
                name: "Features");

            migrationBuilder.DropIndex(
                name: "IX_Navigation_FeatureId",
                table: "Navigation");

            migrationBuilder.DropColumn(
                name: "FeatureId",
                table: "Navigation");

            migrationBuilder.AlterColumn<int>(
                name: "AuthenticationTypeId",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_AuthenticationTypes_AuthenticationTypeId",
                table: "Users",
                column: "AuthenticationTypeId",
                principalTable: "AuthenticationTypes",
                principalColumn: "AuthenticationTypeId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
